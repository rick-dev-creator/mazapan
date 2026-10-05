using System.Text;
using Mazapan.Util;

namespace Mazapan.Coverage;

/// <summary>Toolkits an app can be built with. Plugins declare the ones they theme.</summary>
public static class Toolkit
{
    public const string Terminal = "terminal"; // runs in a terminal: the terminal's colors
    public const string GTK4 = "gtk4"; // libadwaita included
    public const string GTK3 = "gtk3";
    public const string Qt6 = "qt6";
    public const string Qt5 = "qt5";
    public const string Electron = "electron";
    public const string Chromium = "chromium";
    public const string Firefox = "firefox";
    public const string Flatpak = "flatpak"; // sandboxed: host theme files don't reach it
    public const string Web = "web"; // a web app or page: it looks as the browser does
    public const string Other = "other"; // its own UI (Flutter, a web view, a game, kitty's GL…)
}

/// <summary>
/// An app shown in menus. Only Id, Name, Exec, Toolkit and Plugin go to
/// JSON (Apps.Json), in that order, as Go's struct tags said.
/// </summary>
public sealed class App
{
    /// <summary>The .desktop file's id.</summary>
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Exec { get; set; } = "";
    public string Toolkit { get; set; } = "";
    /// <summary>What themes it; "" = nothing yet.</summary>
    public string Plugin { get; set; } = "";
    public bool Terminal { get; set; }
    /// <summary>Its Flatpak id.</summary>
    public string Flatpak { get; set; } = "";
    /// <summary>Opens a URL.</summary>
    public bool WebApp { get; set; }
}

/// <summary>Covers is what a plugin themes: .desktop ids or executable names, and toolkits.</summary>
public sealed record Covers(string Plugin, IReadOnlyList<string> Apps, IReadOnlyList<string> Toolkits);

/// <summary>
/// Finds the installed apps and tells which ones the theme reaches: an app
/// is covered when an enabled plugin themes it, by name or by the toolkit
/// it's built with (GTK 4, Qt 6, a terminal…).
/// </summary>
public static class Apps
{
    /// <summary>
    /// Report lists every app shown in menus, each with its toolkit and the
    /// plugin that themes it, uncovered first. Covers are taken in order: when
    /// two plugins claim the same app or toolkit, the first one gets it.
    /// </summary>
    public static List<App> Report(IEnumerable<string> dirs, string lang, IEnumerable<Covers> covers)
    {
        var apps = Scan(dirs, lang);
        var byApp = new Dictionary<string, string>();
        var byToolkit = new Dictionary<string, string>();
        foreach (var c in covers)
        {
            foreach (var a in c.Apps)
                if (Get(byApp, a) == "") byApp[a] = c.Plugin;
            foreach (var t in c.Toolkits)
                if (Get(byToolkit, t) == "") byToolkit[t] = c.Plugin;
        }
        // Every toolkit first (the ones only a package can tell come from two
        // pacman calls for all of them), then who themes each app.
        var d = new Detector();
        foreach (var a in apps) a.Toolkit = d.ToolkitOf(a);
        d.ResolvePackages(apps);
        foreach (var a in apps)
        {
            if (Get(byApp, a.Id) != "") a.Plugin = byApp[a.Id];
            else if (Get(byApp, Paths.Base(a.Exec)) != "") a.Plugin = byApp[Paths.Base(a.Exec)];
            else a.Plugin = Get(byToolkit, a.Toolkit);
        }
        // sort.SliceStable: uncovered first, then by name ignoring case.
        return apps.OrderBy(a => a.Plugin != "").ThenBy(a => a.Name.ToLowerInvariant(), GoStrings.Comparer).ToList();
    }

    static string Get(Dictionary<string, string> m, string k) => m.TryGetValue(k, out var v) ? v : "";

    /// <summary>
    /// Json is the report as `mazapan coverage --json` prints it, byte for byte
    /// what Go's json.Marshal wrote for []App (without the newline): "[]"
    /// when there are none, plugin left out when empty.
    /// </summary>
    public static string Json(IEnumerable<App> apps)
    {
        var b = new StringBuilder("[");
        var first = true;
        foreach (var a in apps)
        {
            if (!first) b.Append(',');
            first = false;
            b.Append("{\"id\":");
            GoFormat.JsonString(b, a.Id);
            b.Append(",\"name\":");
            GoFormat.JsonString(b, a.Name);
            b.Append(",\"exec\":");
            GoFormat.JsonString(b, a.Exec);
            b.Append(",\"toolkit\":");
            GoFormat.JsonString(b, a.Toolkit);
            if (a.Plugin != "")
            {
                b.Append(",\"plugin\":");
                GoFormat.JsonString(b, a.Plugin);
            }
            b.Append('}');
        }
        return b.Append(']').ToString();
    }

    /// <summary>
    /// Dirs are where .desktop files live, most important first: the person's
    /// own, then the system's (XDG_DATA_DIRS), then Flatpak's.
    /// </summary>
    public static List<string> Dirs()
    {
        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "";
        if (dataHome == "") dataHome = Paths.Join(home, ".local/share");
        var dirs = new List<string> { Paths.Join(dataHome, "applications") };
        var data = Environment.GetEnvironmentVariable("XDG_DATA_DIRS") ?? "";
        if (data == "") data = "/usr/local/share:/usr/share";
        foreach (var d in data.Split(':'))
            if (d != "") dirs.Add(Paths.Join(d, "applications"));
        dirs.Add(Paths.Join(dataHome, "flatpak/exports/share/applications"));
        dirs.Add("/var/lib/flatpak/exports/share/applications");
        return dirs;
    }

    /// <summary>
    /// Scan reads the .desktop files in dirs (subdirectories too: vendor/x.desktop
    /// is "vendor-x"); the first file with an id wins, as in menus, even when it
    /// hides the app. lang ("es_MX") picks the translated name.
    /// </summary>
    public static List<App> Scan(IEnumerable<string> dirs, string lang)
    {
        var desktops = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").Split(':');
        var seen = new HashSet<string>();
        var out_ = new List<App>();
        foreach (var d in dirs)
        {
            var files = new List<string>();
            WalkDir(d, files);
            files.Sort(GoStrings.Comparer);
            foreach (var f in files)
            {
                var id = Rel(d, f);
                if (id.EndsWith(".desktop", StringComparison.Ordinal)) id = id[..^".desktop".Length];
                id = id.Replace('/', '-');
                if (!seen.Add(id)) continue;
                if (Parse(f, lang, desktops) is { } a)
                {
                    a.Id = id;
                    out_.Add(a);
                }
            }
        }
        return out_;
    }

    /// <summary>
    /// WalkDir is filepath.WalkDir collecting the files (anything not a
    /// folder) named *.desktop: in name order, into subfolders but not
    /// through symlinks to folders, not even when root itself is one. What
    /// can't be read is skipped.
    /// </summary>
    static void WalkDir(string root, List<string> files)
    {
        if (!Lstat(root, out var isDir)) return;
        if (!isDir)
        {
            if (root.EndsWith(".desktop", StringComparison.Ordinal)) files.Add(root);
            return;
        }
        Walk(root, files);
    }

    static void Walk(string dir, List<string> files)
    {
        string[] names;
        try
        {
            names = Directory.GetFileSystemEntries(dir).Select(Path.GetFileName).OfType<string>().ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
        Array.Sort(names, GoStrings.Comparer);
        foreach (var n in names)
        {
            var p = Paths.Join(dir, n);
            if (!Lstat(p, out var isDir)) continue;
            if (isDir) Walk(p, files);
            else if (p.EndsWith(".desktop", StringComparison.Ordinal)) files.Add(p);
        }
    }

    /// <summary>Whether path is there, and whether it's a real folder (a symlink isn't).</summary>
    static bool Lstat(string path, out bool isDir)
    {
        isDir = false;
        try
        {
            var fi = new FileInfo(path);
            if (fi.LinkTarget != null) return true;
            if (Directory.Exists(path))
            {
                isDir = true;
                return true;
            }
            return fi.Exists;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>filepath.Rel for a file found under root.</summary>
    static string Rel(string root, string f)
    {
        var r = Paths.Clean(root);
        f = Paths.Clean(f);
        if (f == r) return ".";
        var prefix = r == "/" ? "/" : r + "/";
        return f.StartsWith(prefix, StringComparison.Ordinal) ? f[prefix.Length..] : f;
    }

    internal static App? Parse(string path, string lang, string[] desktops)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
        var code = lang.Split('_', 2)[0];
        var a = new App();
        var names = new Dictionary<string, string>();
        string typ = "", tryExec = "", onlyShowIn = "", notShowIn = "";
        var hidden = false;
        var inEntry = false;
        foreach (var raw in GoStrings.ScanLines(data))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inEntry = line == "[Desktop Entry]";
                continue;
            }
            var eq = line.IndexOf('=');
            if (!inEntry || eq < 0) continue;
            var k = line[..eq].Trim();
            var v = line[(eq + 1)..].Trim();
            if (k == "Type") typ = v;
            else if (k == "Name" || k.StartsWith("Name[", StringComparison.Ordinal)) names[k] = v;
            else if (k == "Exec")
            {
                (a.Exec, a.Terminal, a.Flatpak) = ExecLine.Program(v);
                // a web app launcher with https://…, xdg-open http://…
                a.WebApp = v.Contains("http://") || v.Contains("https://");
            }
            else if (k == "TryExec") tryExec = v;
            else if (k == "Terminal") a.Terminal = a.Terminal || v == "true";
            else if (k == "X-Flatpak") a.Flatpak = v;
            else if (k == "OnlyShowIn") onlyShowIn = v;
            else if (k == "NotShowIn") notShowIn = v;
            else if (k is "NoDisplay" or "Hidden") hidden = hidden || v == "true";
        }
        a.Name = names.GetValueOrDefault("Name", "");
        foreach (var k in new[] { "Name[" + lang + "]", "Name[" + code + "]" })
        {
            if (names.GetValueOrDefault(k, "") != "")
            {
                a.Name = names[k];
                break;
            }
        }
        if (tryExec != "" && Pacman.Exec.LookPath(tryExec) == null) return null; // not installed anymore
        if (onlyShowIn != "" && !AnyIn(onlyShowIn, desktops) || notShowIn != "" && AnyIn(notShowIn, desktops)) return null;
        return typ == "Application" && !hidden && a.Exec != "" && a.Name != "" ? a : null;
    }

    /// <summary>AnyIn: is one of desktops in list ("GNOME;KDE;")?</summary>
    static bool AnyIn(string list, string[] desktops)
    {
        foreach (var x in list.Split(';'))
            foreach (var d in desktops)
                if (x != "" && string.Equals(x, d, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

/// <summary>Go's string handling where .NET's differs.</summary>
internal static class GoStrings
{
    /// <summary>
    /// Comparer orders as Go compares strings: by their UTF-8 bytes, which is
    /// code point order (UTF-16 ordinal differs past U+FFFF).
    /// </summary>
    internal static readonly IComparer<string> Comparer = Comparer<string>.Create(Compare);

    static int Compare(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            char x = a[i], y = b[i];
            if (x == y) continue;
            // A surrogate (above U+FFFF) sorts after every other BMP character.
            var xs = char.IsSurrogate(x);
            var ys = char.IsSurrogate(y);
            if (xs != ys) return xs ? 1 : -1;
            return x < y ? -1 : 1;
        }
        return a.Length.CompareTo(b.Length);
    }

    /// <summary>
    /// ScanLines is bufio.Scanner's default: lines split at \n, a trailing
    /// \r dropped. A line too long for its buffer (64 KiB with the newline)
    /// stops it there, as Go's Scan did.
    /// </summary>
    internal static IEnumerable<string> ScanLines(byte[] data)
    {
        const int maxLine = 64 * 1024 - 1;
        var start = 0;
        while (start < data.Length)
        {
            var nl = Array.IndexOf(data, (byte)'\n', start);
            var end = nl < 0 ? data.Length : nl;
            if (end - start > maxLine) yield break;
            var len = end - start;
            if (len > 0 && data[end - 1] == '\r') len--;
            yield return Files.Utf8.GetString(data, start, len);
            start = end + 1;
        }
    }
}
