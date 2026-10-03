using System.Diagnostics;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    /// <summary>
    /// `mazapan password`: one password changed everywhere it is, in the order
    /// that can't lock anyone out: the disk's first (when it's encrypted),
    /// checked that the new one opens it, then the account's, then the
    /// keyring's (its secrets stay, now opened by the new one). Stopped half
    /// way, running it again finishes it: a step already done is seen as
    /// such. From a terminal it asks; the Settings panel (--gui) gives both
    /// on stdin as JSON {"old", "new"} and reads "step NAME ok|skip|fail".
    /// </summary>
    static int CmdPassword(string[] args)
    {
        if (args is ["root"]) return PasswordRoot();
        var fs = new Flags("password")
            .Bool("gui", "from the Settings panel: both passwords as JSON on stdin, the password for root through polkit")
            .Parse(args);
        var gui = fs.IsSet("gui");
        var user = Environment.GetEnvironmentVariable("USER") is { Length: > 0 } u ? u : Environment.UserName;
        string old, new_;
        if (gui)
        {
            using var doc = JsonDocument.Parse(Console.In.ReadToEnd());
            old = doc.RootElement.GetProperty("old").GetString() ?? "";
            new_ = doc.RootElement.GetProperty("new").GetString() ?? "";
        }
        else
        {
            old = ReadSecret("Current password: ");
            new_ = ReadSecret("New password: ");
            if (ReadSecret("The new one again: ") != new_) throw new MazapanException("the two new passwords aren't the same");
        }
        if (PasswordProblem(new_, DiskDevice() != null) is { } problem)
        {
            Console.WriteLine("fail check " + problem);
            throw new MazapanException(problem);
        }

        // Disk and account: as root (polkit from the panel, sudo from a terminal).
        var psi = new ProcessStartInfo(gui ? "pkexec" : "sudo") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true };
        psi.ArgumentList.Add(Environment.ProcessPath ?? "/usr/bin/mazapan");
        psi.ArgumentList.Add("password");
        psi.ArgumentList.Add("root");
        string rootOut;
        int code;
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Write(JsonSerializer.Serialize(new Dictionary<string, string> { ["user"] = user, ["old"] = old, ["new"] = new_ }, PasswordJson.Default.DictionaryStringString));
            p.StandardInput.Close();
            rootOut = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            code = p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new MazapanException((gui ? "pkexec" : "sudo") + ": " + e.Message);
        }
        Console.Write(rootOut);
        if (code != 0)
            throw new MazapanException(rootOut.Split('\n').FirstOrDefault(l => l.StartsWith("fail ", StringComparison.Ordinal))?[5..] is { } why
                ? why : "nothing was changed (the password for root wasn't given?)");

        // The keyring: the person's own, through its daemon, in their session.
        var keyring = Paths.ExpandHome("~/.local/share/keyrings/login.keyring");
        if (!File.Exists(keyring)) Console.WriteLine("step keyring skip no login keyring");
        else
        {
            var r = Setup.KeyringControl.Change(old, new_);
            // Denied: already the new one (a second run)? Then it's done.
            if (r == Setup.KeyringControl.Result.Denied && Setup.KeyringControl.Change(new_, new_) == Setup.KeyringControl.Result.Ok)
                r = Setup.KeyringControl.Result.Ok;
            Console.WriteLine(r switch
            {
                Setup.KeyringControl.Result.Ok => "step keyring ok",
                // Not running: the next login opens it with the old one once and asks.
                Setup.KeyringControl.Result.NoDaemon => "step keyring skip not running",
                _ => "step keyring fail " + r.ToString().ToLowerInvariant(),
            });
        }
        if (!gui) Console.WriteLine("The password is changed.");
        return 0;
    }

    /// <summary>What a new password can't be (null: it's fine).</summary>
    internal static string? PasswordProblem(string password, bool encrypted)
    {
        if (password == "") return "the new password is empty";
        if (password.Contains('\n') || password.Contains('\0')) return "the new password has a line break in it";
        // Typed as the computer starts, before anything but the console's
        // keyboard: as the installer says.
        if (encrypted && !password.All(c => c >= ' ' && c <= '~'))
            return "with an encrypted disk the password is typed as the computer starts: letters without accents, digits and symbols only";
        return null;
    }

    static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var s = new System.Text.StringBuilder();
        if (!IsTerminal(0))
        {
            var line = Console.In.ReadLine() ?? "";
            Console.WriteLine();
            return line;
        }
        while (true)
        {
            var k = Console.ReadKey(intercept: true);
            if (k.Key == ConsoleKey.Enter) break;
            if (k.Key == ConsoleKey.Backspace) { if (s.Length > 0) s.Length--; continue; }
            if (!char.IsControl(k.KeyChar)) s.Append(k.KeyChar);
        }
        Console.WriteLine();
        return s.ToString();
    }

    /// <summary>
    /// The LUKS partition the root filesystem is on (lsblk, from / down to
    /// what it sits on), or null when the root isn't encrypted.
    /// </summary>
    static string? DiskDevice()
    {
        var src = Output("findmnt", "-no", "SOURCE", "/").Trim();
        var bracket = src.IndexOf('[');
        if (bracket > 0) src = src[..bracket];
        if (src == "") return null;
        foreach (var line in Output("lsblk", "-nlps", "-o", "PATH,FSTYPE", src).Split('\n'))
            if (line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var path, "crypto_LUKS"]) return path;
        return null;
    }

    static string Output(string file, params string[] args)
    {
        var (code, out_, _) = AppsCapture(file, args);
        return code == 0 ? out_ : "";
    }

    /// <summary>
    /// `mazapan password root`: the disk and the account, as root, for the
    /// person who asked (pkexec's or sudo's caller, nobody else's account).
    /// </summary>
    static int PasswordRoot()
    {
        if (GetEuid() != 0)
            throw new MazapanException("password root runs as root (mazapan password)");
        string user, old, new_;
        using (var doc = JsonDocument.Parse(Console.In.ReadToEnd()))
        {
            user = doc.RootElement.GetProperty("user").GetString() ?? "";
            old = doc.RootElement.GetProperty("old").GetString() ?? "";
            new_ = doc.RootElement.GetProperty("new").GetString() ?? "";
        }
        // Only one's own: the uid that asked (pkexec, sudo) is the account's.
        var caller = Environment.GetEnvironmentVariable("PKEXEC_UID") ?? Environment.GetEnvironmentVariable("SUDO_UID") ?? "";
        var uid = Output("id", "-u", "--", user).Trim();
        if (caller == "" || uid == "" || caller != uid)
        {
            Console.WriteLine("fail account only your own password");
            return 1;
        }
        var disk = DiskDevice();
        if (PasswordProblem(new_, disk != null) is { } problem)
        {
            Console.WriteLine("fail check " + problem);
            return 1;
        }

        // The disk: changed only from the current password, and kept only once
        // the new one is seen to open it.
        if (disk == null) Console.WriteLine("step disk skip not encrypted");
        else
        {
            // In memory only (/run is tmpfs), never on a disk.
            var dir = Directory.CreateDirectory(Path.Join("/run", "mazapan-password-" + Guid.NewGuid().ToString("N")),
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            try
            {
                string Key(string name, string value)
                {
                    var f = Path.Join(dir.FullName, name);
                    File.WriteAllBytes(f, Files.Utf8.GetBytes(value)); // the whole file is the key: no newline
                    File.SetUnixFileMode(f, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    return f;
                }
                bool Opens(string keyFile) => AppsCapture("cryptsetup", "open", "--test-passphrase", "--key-file", keyFile, disk).Code == 0;
                var oldKey = Key("old", old);
                var newKey = Key("new", new_);
                if (Opens(newKey) && !Opens(oldKey)) Console.WriteLine("step disk ok already");
                else if (!Opens(oldKey))
                {
                    Console.WriteLine("fail disk the current password doesn't open the disk");
                    return 1;
                }
                else
                {
                    var (c, _, err) = AppsCapture("cryptsetup", "luksChangeKey", "--key-file", oldKey, disk, newKey);
                    if (c != 0 || !Opens(newKey))
                    {
                        Console.WriteLine("fail disk " + (c != 0 ? err.Trim().Split('\n').LastOrDefault() ?? "cryptsetup failed" : "the new password doesn't open it"));
                        return 1;
                    }
                    Console.WriteLine("step disk ok");
                }
            }
            finally
            {
                dir.Delete(true);
            }
        }

        // The account: chpasswd, the new one on stdin.
        var psi = new ProcessStartInfo("chpasswd") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi)!)
        {
            p.StandardInput.Write($"{user}:{new_}\n");
            p.StandardInput.Close();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                Console.WriteLine("fail account " + err.Trim());
                return 1;
            }
        }
        Console.WriteLine("step account ok");
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
    static extern uint GetEuid();
}

[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class PasswordJson : System.Text.Json.Serialization.JsonSerializerContext;
