using System.Diagnostics;
using System.Text.RegularExpressions;
using Mazapan.Setup;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// mazapan install: this system onto a disk, from the ISO (the installer
/// panel runs it). archinstall does the work, from the configuration
/// Setup.Archinstall writes.
///
/// For the panel, "run" says as it goes: "step NAME" (disk, base, boot,
/// account, desktop, finish), "progress N OF" while packages go in, "done"
/// at the end, or "fail KIND DETAIL" (answers, disk, uefi, archinstall).
/// Everything archinstall says is in /var/log/mazapan-install.log.
/// </summary>
public static partial class Program
{
    const string InstallLog = "/var/log/mazapan-install.log";

    static int CmdInstall(string[] args)
    {
        var sub = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "";
        var fs = new Flags("install " + sub)
            .Bool("json", "as JSON, for the installer")
            .Parse(args.Length > 0 ? args[1..] : []);
        return sub switch
        {
            "disks" => InstallDisks(fs.IsSet("json")),
            "plan" when fs.Rest.Count == 1 => InstallPlan(fs.Rest[0]),
            "run" when fs.Rest.Count == 1 => InstallRun(fs.Rest[0]),
            "keys" => InstallKeys(fs.IsSet("json")),
            _ => throw new MazapanException("usage: mazapan install disks [--json] | plan ANSWERS.json | run ANSWERS.json | keys [--json]"),
        };
    }

    static List<Disk> ListDisks()
    {
        var (code, out_, err) = AppsCapture("lsblk", Disks.LsblkArgs);
        if (code != 0) throw new MazapanException("lsblk: " + err.Trim());
        return Disks.Parse(out_);
    }

    static int InstallDisks(bool json)
    {
        var disks = ListDisks();
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(disks.Select(d => (object)new Fields
            {
                { "path", d.Path }, { "size", d.Size }, { "model", d.Model }, { "transport", d.Transport },
                { "removable", d.Removable }, { "partitions", d.Partitions }, { "systems", d.Systems }, { "too_small", d.TooSmall },
            }).ToList()));
            return 0;
        }
        foreach (var d in disks)
            Console.WriteLine($"{d.Path,-14} {Mb(d.Size),9}  {(d.Model == "" ? "-" : d.Model)}" +
                (d.Systems.Count > 0 ? $"  (has {string.Join(", ", d.Systems)})" : "") + (d.TooSmall ? "  too small" : ""));
        return 0;
    }

    /// <summary>The answers, checked against this machine too: the disk is one of ListDisks', the zone and locale exist.</summary>
    static (Answers, Disk) ReadAnswers(string path)
    {
        var a = Answers.Parse(File.ReadAllText(path));
        var disk = ListDisks().FirstOrDefault(d => d.Path == a.Disk)
            ?? throw new MazapanException($"answers: {a.Disk} isn't a disk to install on (mazapan install disks)");
        if (disk.TooSmall) throw new MazapanException($"answers: {a.Disk} is too small");
        if (!File.Exists("/usr/share/zoneinfo/" + a.Timezone)) throw new MazapanException($"answers: no time zone {a.Timezone}");
        static string Read(string f) => File.Exists(f) ? File.ReadAllText(f) : "";
        var zoneTab = Read("/usr/share/zoneinfo/zone1970.tab");
        if (Locales.Country(a.Timezone, zoneTab) is var cc && cc.Length == 2 && cc.All(char.IsAsciiLetterUpper)) a.Country = cc;
        // No theme said (the installer couldn't tell): the first there is.
        if (a.Theme == "")
            a.Theme = Directory.Exists(Paths.Join(Root(), "themes"))
                ? Directory.GetDirectories(Paths.Join(Root(), "themes")).Select(Paths.Base).Order(StringComparer.Ordinal).FirstOrDefault() ?? ""
                : "";
        if (a.Theme == "" || !Directory.Exists(Paths.Join(Root(), "themes", a.Theme))) throw new MazapanException($"answers: no theme {a.Theme}");
        if (a.Locale == "") a.Locale = Locales.For(a.Language, a.Timezone, zoneTab, Read("/usr/share/i18n/SUPPORTED"));
        if (File.Exists("/usr/share/i18n/SUPPORTED") && !File.ReadLines("/usr/share/i18n/SUPPORTED").Any(l => l.StartsWith(a.Locale + " ")))
            throw new MazapanException($"answers: no locale {a.Locale}");
        var (_, apps) = Store.AppCatalog.Load(Paths.Join(Root(), "catalog", "apps.toml"));
        if (a.Apps.FirstOrDefault(id => apps.All(x => x.Id != id)) is { } unknown) throw new MazapanException($"answers: no app {unknown} in the catalog");
        return (a, disk);
    }

    static string Keymap(Answers a)
    {
        const string map = "/usr/share/systemd/kbd-model-map";
        return Archinstall.ConsoleKeymap(File.Exists(map) ? File.ReadAllText(map) : "", a.KbLayout, a.KbVariant);
    }

    static int InstallPlan(string path)
    {
        var (a, disk) = ReadAnswers(path);
        Console.Write(Archinstall.Config(a, disk.Size, Keymap(a)));
        return 0;
    }

    [GeneratedRegex(@"\((\s*\d+)/(\d+)\)\s+(installing|upgrading|reinstalling)\s")] private static partial Regex PacmanProgress();

    /// <summary>The steps, in the order archinstall takes them; a step is only ever followed by a later one.</summary>
    static readonly string[] InstallOrder = ["disk", "base", "boot", "account", "desktop", "finish"];

    /// <summary>
    /// The step a line of archinstall's starts, or null. Its own lines,
    /// matched whole where a package's could look the same (systemd-sysusers
    /// says "Creating user 'x'" for every system user).
    /// </summary>
    public static string? InstallStep(string line, string user) =>
        line.StartsWith("Wiping partitions") || line.StartsWith("Creating partitions") ? "disk"
        : line.StartsWith("Installing packages: ['base'") ? "base"
        : line.StartsWith("Adding bootloader") ? "boot"
        : line.Trim() == "Creating user " + user ? "account"
        : line.StartsWith("Installing packages: ['mazapan'") ? "desktop"
        : line.StartsWith("Executing custom command") ? "finish"
        : null;

    const string RootKeys = "/root/.ssh/authorized_keys";

    /// <summary>
    /// The SSH keys the new account will let in (the live system's root's,
    /// there only when someone set it up to be reached over SSH): their
    /// comments, for the installer's review.
    /// </summary>
    static int InstallKeys(bool json)
    {
        if (!Environment.IsPrivilegedProcess) throw new MazapanException("keys: as root (sudo mazapan install keys)");
        var keys = File.Exists(RootKeys) ? Answers.Keys(File.ReadAllText(RootKeys)) : [];
        var names = keys.Select(k => k.Split(' ') is { Length: > 2 } f ? string.Join(' ', f[2..]) : k.Split(' ')[0]).ToList();
        if (json) Console.WriteLine(GoJson.Marshal(names));
        else foreach (var n in names) Console.WriteLine(n);
        return 0;
    }

    static int InstallRun(string path)
    {
        if (!Environment.IsPrivilegedProcess) throw new MazapanException("install runs as root (sudo mazapan install run …)");
        // Only from the ISO: anywhere else the disks it would offer are the
        // running system's, and the path it's given would be deleted.
        if (!Hardware.ThisMachine.Get().Live) throw new MazapanException("install runs on the ISO's live system only");
        Answers a;
        Disk disk;
        try
        {
            (a, disk) = ReadAnswers(path);
        }
        catch (MazapanException e)
        {
            Console.WriteLine("fail answers " + e.Message.Replace('\n', ' '));
            throw;
        }
        finally
        {
            // It has the password: read once, gone.
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        // Whoever set the live system up to be reached over SSH (cidata) can
        // reach the new one the same way.
        if (File.Exists(RootKeys)) a.SshKeys = Answers.Keys(File.ReadAllText(RootKeys));
        // This machine's hardware plugins: its hardware is seen from here (the
        // same machine), how the system goes on it is the install's. Their
        // packages come from the ISO's repository with the rest.
        var target = Hardware.ThisMachine.Get().Installed("btrfs", "grub", bootOnRoot: !a.Encrypt);
        foreach (var p in Plugins.Plugin.Discover(PluginDirs()).Plugins)
            if (p.Hardware is { } h && !h.OnlyAny && !h.Live && h.Matches(target).Ok)
            {
                a.Hardware.Add(p.Id);
                a.HardwarePackages.AddRange(p.Pacman.Where(x => !a.HardwarePackages.Contains(x)));
            }
        a.Check();
        if (!Directory.Exists("/sys/firmware/efi"))
        {
            Console.WriteLine("fail uefi");
            throw new MazapanException("this machine started without UEFI: only UEFI is supported");
        }

        var dir = "/run/mazapan-install";
        Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var config = Paths.Join(dir, "config.json");
        var creds = Paths.Join(dir, "creds.json");
        Files.WriteAtomic(config, Archinstall.Config(a, disk.Size, Keymap(a)));
        File.WriteAllText(creds, "");
        File.SetUnixFileMode(creds, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.WriteAllText(creds, Archinstall.Creds(a));

        int code;
        try
        {
            Console.WriteLine("step disk");
            Leftovers(a.Disk);
            code = RunArchinstall(config, creds, a.User);
        }
        finally
        {
            File.Delete(creds);
        }
        if (code != 0)
        {
            Console.WriteLine($"fail archinstall {code}");
            throw new MazapanException($"archinstall stopped ({code}): {InstallLog}");
        }
        Console.WriteLine("done");
        return 0;
    }

    /// <summary>
    /// What a failed install left on the disk (mounted at /mnt, swap, LUKS
    /// opened): taken down, or the next one finds it busy.
    /// </summary>
    static void Leftovers(string disk)
    {
        if (File.ReadLines("/proc/mounts").Any(l => l.Split(' ') is [_, var m, ..] && (m == "/mnt" || m.StartsWith("/mnt/"))))
            AppsRun("umount", "-R", "/mnt");
        var (code, out_, _) = AppsCapture("lsblk", "-lnp", "-o", "PATH,TYPE,FSTYPE", disk);
        if (code != 0) return;
        foreach (var f in out_.Split('\n').Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            if (f.Length >= 3 && f[2] == "swap") AppsRun("swapoff", f[0]);
            if (f.Length >= 2 && f[1] == "crypt") AppsRun("cryptsetup", "close", f[0]);
        }
    }

    static int RunArchinstall(string config, string creds, string user)
    {
        var psi = new ProcessStartInfo("archinstall")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
        };
        foreach (var x in (string[])["--config", config, "--creds", creds, "--silent", "--offline", "--skip-ntp", "--skip-wkd", "--skip-version-check"])
            psi.ArgumentList.Add(x);
        psi.Environment["TERM"] = "dumb";
        using var log = new StreamWriter(InstallLog, append: true) { AutoFlush = true };
        log.WriteLine($"== {DateTime.Now:O} archinstall {string.Join(' ', psi.ArgumentList)}");
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var step = "disk";
        var gate = new object();
        void Line(string? l)
        {
            if (l == null) return;
            lock (gate)
            {
                log.WriteLine(l);
                if (InstallStep(l, user) is { } s && Array.IndexOf(InstallOrder, s) > Array.IndexOf(InstallOrder, step))
                {
                    step = s;
                    Console.WriteLine("step " + s);
                }
                if (PacmanProgress().Match(l) is { Success: true } m)
                    Console.WriteLine($"progress {m.Groups[1].Value.Trim()} {m.Groups[2].Value}");
            }
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data);
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        p.WaitForExit();
        return p.ExitCode;
    }
}
