using System.Diagnostics;
using Mazapan.Util;

namespace Mazapan.Applying;

/// <summary>
/// Root does what needs root, through sudo: system files (/etc/modprobe.d/
/// mazapan-…), their reload commands, packages. Only `mazapan apply --system`
/// and `mazapan undo` get here: a plain apply (the theme picker's, an
/// agent's) never asks for a password. sudo asks on the terminal; without
/// one it must not need to (sudo -n), or it fails and says so.
/// </summary>
public static partial class AsRoot
{
    /// <summary>
    /// IsSystem: a file mazapan may write as root: a mazapan* file right in one
    /// of the drop-in folders (Plugin.SystemDirs), the path as clean as it is
    /// real. Every root write and delete checks it, whatever asked for it:
    /// owned.json and snapshots are the person's files, so a path from them
    /// must never be enough to touch /etc/pacman.conf.
    /// </summary>
    public static bool IsSystem(string path)
    {
        if (path != Paths.Clean(path) || !path.StartsWith('/')) return false;
        var dir = Paths.Dir(path);
        var name = Paths.Base(path);
        // PAM: only the login screen's own service, never another name.
        if (dir == "/etc/pam.d" && name != "mazapan-greetd") return false;
        return Plugins.Plugin.SystemDirs.Contains(dir) && name.StartsWith("mazapan") &&
            (Paths.Real(dir) is not { } real || real == dir);
    }

    static void Guard(string path)
    {
        if (!IsSystem(path)) throw new MazapanException($"{path}: not a file mazapan writes as root (a mazapan* drop-in in {string.Join(", ", Plugins.Plugin.SystemDirs)})");
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[a-z0-9@_+][a-z0-9@._+-]*\z")]
    private static partial System.Text.RegularExpressions.Regex PackageName();

    /// <summary>A package's name, as pacman spells them: never an option (--config=…).</summary>
    public static bool IsPackage(string name) => PackageName().IsMatch(name);

    static void GuardPackages(IEnumerable<string> packages)
    {
        foreach (var p in packages)
            if (!IsPackage(p)) throw new MazapanException($"\"{p}\" isn't a package name");
    }

    static bool Tty() => Console.IsInputRedirected == false;

    /// <summary>Runs a command as root; its output goes to the terminal. Returns the exit status.</summary>
    public static int Sudo(params string[] args)
    {
        var tty = Tty();
        // Without a terminal, what it prints goes to stderr: stdout can be a
        // protocol (mazapan mcp), and a password prompt must not wait there.
        var psi = new ProcessStartInfo("sudo") { UseShellExecute = false, RedirectStandardOutput = !tty, RedirectStandardInput = !tty };
        if (!tty) psi.ArgumentList.Add("-n");
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            if (!tty)
            {
                p.StandardInput.Close();
                var copy = p.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardError());
                p.WaitForExit();
                copy.Wait(TimeSpan.FromSeconds(5));
            }
            else p.WaitForExit();
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            throw new MazapanException("sudo: " + e.Message);
        }
    }

    static void Must(string what, params string[] args)
    {
        var code = Sudo(args);
        if (code != 0) throw new MazapanException($"{what}: sudo {string.Join(" ", args)} failed (exit status {code})");
    }

    /// <summary>Writes a system file as root: root-owned, 0644, whole (install writes it anew).</summary>
    public static void Write(string path, byte[] content)
    {
        Guard(path);
        Install(path, content);
    }

    /// <summary>
    /// Points pacman at Mazapán's own repository (Pacman.Repository): its
    /// mirrorlist written whole, and pacman.conf given the [mazapan] section
    /// that includes it when it hasn't one. The only root writes outside the
    /// drop-in folders, to fixed paths, with text made here.
    /// </summary>
    public static void UseRepository(string mirrorlist)
    {
        // Without Mazapán's keys in pacman's keyring, its signed packages
        // would stop every update, Arch's too.
        if (!File.Exists(Pacman.Repository.Keyring))
            throw new MazapanException("Mazapán's keys aren't installed (mazapan-keyring): its repository can't be trusted yet");
        Must("putting Mazapán's keys in pacman's keyring", "pacman-key", "--populate", "mazapan");
        Install(Pacman.Repository.Mirrorlist, Files.Utf8.GetBytes(mirrorlist));
        Must("adding [mazapan] to /etc/pacman.conf", "sh", "-c", Pacman.Repository.EnableScript);
    }

    static void Install(string path, byte[] content)
    {
        // Staged in a folder only this user can enter, created fresh, 0600.
        var dir = Directory.CreateTempSubdirectory("mazapan-root-");
        var tmp = Path.Join(dir.FullName, "file");
        try
        {
            using (var f = new FileStream(tmp, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }))
                f.Write(content);
            Must($"writing {path}", "install", "-D", "-m", "0644", "-o", "root", "-g", "root", "-T", "--", tmp, path);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    public static void Remove(string path)
    {
        Guard(path);
        Must($"removing {path}", "rm", "-f", "--", path);
    }

    /// <summary>A reload command, as root; its failure is a warning, as a user file's is.</summary>
    public static string? Run(string command) =>
        Sudo("sh", "-c", command) is var code and not 0 ? $"exit status {code}" : null;

    /// <summary>The packages of these that aren't installed (pacman -T: what it can't satisfy).</summary>
    public static List<string> Missing(IEnumerable<string> packages)
    {
        var want = packages.Distinct().Order(StringComparer.Ordinal).ToList();
        GuardPackages(want);
        if (want.Count == 0 || !File.Exists("/usr/bin/pacman")) return [];
        var psi = new ProcessStartInfo("pacman") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.ArgumentList.Add("-T");
        psi.ArgumentList.Add("--");
        foreach (var w in want) psi.ArgumentList.Add(w);
        using var p = Process.Start(psi)!;
        var out_ = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        p.WaitForExit();
        return out_.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToList();
    }

    /// <summary>Installs packages (pacman asks on the terminal, as it always does).</summary>
    public static void Install(List<string> packages)
    {
        if (packages.Count == 0) return;
        GuardPackages(packages);
        // --system is the yes to this list; without a terminal pacman couldn't ask anyway.
        Must("installing packages", Tty() ? ["pacman", "-S", "--needed", "--", .. packages] : ["pacman", "-S", "--needed", "--noconfirm", "--", .. packages]);
    }

    /// <summary>
    /// Uninstalls what an apply installed (undo): only what's still installed
    /// and nothing else needs (pacman refuses the whole list otherwise). What
    /// can't go is said, not a failure: the rest of the undo is done.
    /// </summary>
    public static List<string> Uninstall(List<string> packages)
    {
        var kept = new List<string>();
        var go = new List<string>();
        foreach (var p in packages.Where(IsPackage).Distinct())
        {
            var info = Query("-Qi", p);
            if (info == null) continue; // not installed any more
            var required = info.Split('\n').FirstOrDefault(l => l.StartsWith("Required By"))?.Split(':', 2)[1].Trim();
            if (required is null or "None") go.Add(p);
            else kept.Add($"{p} (needed by {required})");
        }
        if (go.Count > 0 && Sudo(Tty() ? ["pacman", "-R", "--", .. go] : ["pacman", "-R", "--noconfirm", "--", .. go]) != 0)
            kept.AddRange(go.Select(p => $"{p} (pacman -R failed)"));
        return kept;
    }

    static string? Query(params string[] args)
    {
        var psi = new ProcessStartInfo("pacman") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        psi.Environment["LC_ALL"] = "C";
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var out_ = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode == 0 ? out_ : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
