using Mazapan.Applying;

namespace Mazapan.Util;

/// <summary>
/// LegacyName moves what myarch (Mazapán's name until 2026-09-30) kept to
/// Mazapán's places, once, the first time Mazapán runs: its configuration,
/// state and data folders (~/.config/myarch, ~/.local/state/myarch-modes…),
/// and the web apps' launchers. owned.json then says where the moved files
/// are; what isn't moved (the generated files in the apps' own folders,
/// system files) stays owned where it is, so the next apply takes it away
/// as what's no longer written (an orphan) and writes Mazapán's.
/// </summary>
public static partial class LegacyName
{
    const string Old = "myarch", New = "mazapan";

    /// <summary>
    /// What myarch kept in the folders of Roots, by name: nothing else of
    /// the person's is touched (a ~/.config/myarch-notes of theirs stays).
    /// </summary>
    static readonly string[] Names =
    [
        "myarch", "myarch-apply.log", "myarch-clipboard", "myarch-emoji", "myarch-first-apply.log",
        "myarch-markets", "myarch-modes", "myarch-notifications", "myarch-wallpaper", "myarch-webapps",
    ];

    /// <summary>The folders the old name's folders sit in.</summary>
    static string[] Roots() =>
    [
        Paths.ExpandHome("~/.config"), Paths.ExpandHome("~/.local/state"),
        Paths.ExpandHome("~/.local/share"), Paths.ExpandHome("~/.cache"),
    ];

    /// <summary>~/.config/myarch: moved last, so once it's Mazapán's the rest is too.</summary>
    static string Marker(string name) => Paths.ExpandHome("~/.config/" + name);

    static bool Pending() => Directory.Exists(Marker(Old)) && !Paths.Exists(Marker(New));

    /// <summary>
    /// Migrate moves what's myarch's. Stopped halfway (a crash, a kill), the
    /// next run carries on: every step is skipped once done, and
    /// ~/.config/myarch, the last, says it's all done.
    /// </summary>
    public static void Migrate()
    {
        if (!Pending()) return;
        // Two mazapan at once (the shell starts several): one moves, the other waits.
        using var held = Lock();
        if (held == null || !Pending()) return;
        foreach (var root in Roots())
            foreach (var name in Names)
            {
                var from = Paths.Join(root, name);
                if (from == Marker(Old)) continue;
                Move(from, Paths.Join(root, New + name[Old.Length..]));
            }
        WebApps();
        // The files it owns that moved with their folders: owned where they are now.
        var ownedPath = Apply.StatePath();
        if (File.Exists(ownedPath))
        {
            var owned = Apply.LoadOwned(ownedPath);
            var next = new Owned();
            foreach (var (path, sum) in owned)
            {
                var now = Moved(path);
                next[now != path && !Paths.Exists(path) && Paths.Exists(now) ? now : path] = sum;
            }
            next.Save(ownedPath);
        }
        if (Move(Marker(Old), Marker(New)))
            Console.Error.WriteLine("mazapan: myarch's folders are Mazapán's now (the name changed); `mazapan apply` writes the rest");
        try { File.Delete(held.Name); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Where a path in one of myarch's folders is once they're moved.</summary>
    static string Moved(string path)
    {
        foreach (var root in Roots())
            foreach (var name in Names)
            {
                var from = Paths.Join(root, name);
                if (path == from || path.StartsWith(from + "/", StringComparison.Ordinal))
                    return Paths.Join(root, New + name[Old.Length..]) + path[from.Length..];
            }
        return path;
    }

    static bool Move(string from, string to)
    {
        if (!Paths.Exists(from) || Paths.Exists(to)) return false;
        try
        {
            if (Directory.Exists(from) && new FileInfo(from).LinkTarget == null) Directory.Move(from, to);
            else File.Move(from, to);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"mazapan: couldn't move {Paths.Tilde(from)} to {Paths.Tilde(to)}: {e.Message}");
            return false;
        }
    }

    /// <summary>The lock, taken (waiting up to 10 s for another mazapan), or null.</summary>
    static FileStream? Lock()
    {
        var path = Paths.ExpandHome("~/.config/.mazapan-rename.lock");
        for (var i = 0; i < 100; i++)
        {
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { return null; }
        }
        return null;
    }

    /// <summary>The web apps' launchers (myarch-webapp-ID.desktop) and what they point to.</summary>
    static void WebApps()
    {
        var apps = Paths.ExpandHome("~/.local/share/applications");
        if (!Directory.Exists(apps)) return;
        foreach (var from in Directory.GetFiles(apps, Old + "-webapp-*.desktop"))
        {
            var to = Paths.Join(apps, New + Paths.Base(from)[Old.Length..]);
            if (File.Exists(to)) continue;
            try
            {
                var text = Path(File.ReadAllText(from)).Replace("X-MyArch-WebApp", "X-Mazapan-WebApp").Replace(Old + "-webapps", New + "-webapps");
                Files.WriteAtomic(to, text);
                File.Delete(from);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// Old is a plugin's path (or command) from before the name changed,
    /// in Mazapán's places: a plugin from git that still writes
    /// ~/.config/hypr/myarch/x.lua writes where it's read now.
    /// </summary>
    public static string Path(string old) => OldPlace().Replace(old, "${1}" + New);

    [System.Text.RegularExpressions.GeneratedRegex(@"(/\.(?:config/hypr|config/quickshell|local/share)/)myarch(?=/)|(?<=(?:^|[\s:])/etc/(?:[\w.-]+/)+)myarch")]
    private static partial System.Text.RegularExpressions.Regex OldPlace();

    /// <summary>
    /// A file of myarch's login screen that greetd, started before the
    /// name changed, still runs on (its configuration, greeter, PAM
    /// service): taken away it would leave no login screen after a log
    /// out. It goes on an apply after the next boot.
    /// </summary>
    public static bool LoginUses(string path)
    {
        if (!path.StartsWith("/etc/greetd/" + Old, StringComparison.Ordinal) && path != "/etc/pam.d/" + Old + "-greetd") return false;
        try
        {
            foreach (var d in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Paths.Base(d), out _)) continue;
                try
                {
                    if (File.ReadAllText(d + "/cmdline").Contains("/etc/greetd/" + Old + ".toml")) return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return false;
    }

    /// <summary>
    /// Refuses to put back files from before the name changed (an undo, a
    /// rollback): they'd be myarch's, in places nothing reads any more.
    /// </summary>
    public static void RefuseOld(IEnumerable<string> paths, string what)
    {
        if (paths.Any(p => Path(p) != p || Moved(p) != p))
            throw new MazapanException($"{what} is from before the name changed to Mazapán: its files are myarch's, so it can't be put back");
    }

        /// <summary>A system file myarch wrote (/etc/modprobe.d/myarch-x.conf): taken away once Mazapán writes its own.</summary>
    public static bool IsOldSystemName(string name) => name.StartsWith(Old, StringComparison.Ordinal);
}
