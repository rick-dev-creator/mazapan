using System.Text.RegularExpressions;
using MyArch.Pacman;
using MyArch.Util;

namespace MyArch.Coverage;

/// <summary>
/// Detector tells an app's toolkit from its binary (the libraries it links,
/// the files next to it), a script's imports, or its pacman package.
/// </summary>
internal sealed partial class Detector
{
    /// <summary>Known are apps whose toolkit their binary doesn't tell.</summary>
    static readonly Dictionary<string, string> Known = new()
    {
        ["firefox"] = Toolkit.Firefox, ["librewolf"] = Toolkit.Firefox, ["zen-browser"] = Toolkit.Firefox,
        ["zen-bin"] = Toolkit.Firefox, ["zen"] = Toolkit.Firefox, ["floorp"] = Toolkit.Firefox,
        ["mullvad-browser"] = Toolkit.Firefox, ["thunderbird"] = Toolkit.Firefox, ["waterfox"] = Toolkit.Firefox,
        ["chromium"] = Toolkit.Chromium, ["google-chrome-stable"] = Toolkit.Chromium, ["brave"] = Toolkit.Chromium,
        ["vivaldi-stable"] = Toolkit.Chromium, ["microsoft-edge-stable"] = Toolkit.Chromium, ["spotify"] = Toolkit.Chromium,
        ["code"] = Toolkit.Electron, ["codium"] = Toolkit.Electron, ["cursor"] = Toolkit.Electron,
        ["discord"] = Toolkit.Electron, ["slack"] = Toolkit.Electron, ["obsidian"] = Toolkit.Electron,
        ["signal-desktop"] = Toolkit.Electron,
        ["kitty"] = Toolkit.Other, ["alacritty"] = Toolkit.Other, ["wezterm"] = Toolkit.Other, ["ghostty"] = Toolkit.Other,
    };

    /// <summary>Resolved path -> toolkit ("" = ask pacman).</summary>
    internal readonly Dictionary<string, string> ByPath = new();

    internal string ToolkitOf(App a)
    {
        if (a.Flatpak != "") return Toolkit.Flatpak;
        if (a.Terminal) return Toolkit.Terminal;
        if (a.WebApp) return Toolkit.Web;
        if (Known.TryGetValue(Paths.Base(a.Exec), out var k)) return k;
        var path = Exec.LookPath(a.Exec);
        if (path == null) return Toolkit.Other;
        path = Paths.Real(path) ?? path;
        if (ByPath.TryGetValue(path, out var t)) return t;
        t = FromBinary(path, 0);
        ByPath[path] = t;
        return t; // "": decided later from its package (ResolvePackages)
    }

    /// <summary>
    /// Web engines first: Electron, Chromium and Firefox builds link GTK for
    /// their dialogs, but a GTK theme doesn't reach what they draw.
    /// </summary>
    internal static string FromBinary(string path, int depth)
    {
        if (Known.TryGetValue(Paths.Base(path), out var k)) return k;
        var dir = Paths.Dir(path);
        bool Has(params string[] names) =>
            names.Any(n => File.Exists(Paths.Join(dir, n)) || Directory.Exists(Paths.Join(dir, n)));
        var elf = Elf.Open(path);
        if (elf != null)
        {
            string all;
            try
            {
                all = string.Join(" ", elf.ImportedLibraries() ?? []);
            }
            finally
            {
                elf.Close();
            }
            if (Has("resources/app.asar", "resources/app")) return Toolkit.Electron;
            if (all.Contains("libffmpeg") || all.Contains("libcef") || Has("v8_context_snapshot.bin", "chrome_100_percent.pak"))
                return Toolkit.Chromium;
            if (Has("libxul.so", "application.ini")) return Toolkit.Firefox;
            if (all.Contains("libflutter") || all.Contains("webkit")) return Toolkit.Other;
            return FromLibs(all);
        }
        // A script: what it imports, or the program it hands over to.
        byte[] b;
        try
        {
            using var f = new FileStream(path, FileMode.Open, FileAccess.Read);
            b = new byte[1 << 16];
            var n = 0;
            int r;
            while (n < b.Length && (r = f.Read(b, n, b.Length - n)) > 0) n += r;
            Array.Resize(ref b, n);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ""; }
        if (b.Length < 2 || b[0] != '#' || b[1] != '!') return "";
        var s = Files.Utf8.GetString(b);
        if (WebAppRun().IsMatch(s)) return Toolkit.Web;
        if (ElectronRun().IsMatch(s)) return Toolkit.Electron;
        if (Gtk4Import().IsMatch(s)) return Toolkit.GTK4;
        if (QtImport().Match(s) is { Success: true } qt) return qt.Value.Contains('6') ? Toolkit.Qt6 : Toolkit.Qt5;
        if (GtkImport().IsMatch(s)) return Toolkit.GTK3;
        if (ExecTarget().Match(s) is { Success: true } m && depth < 2) return FromBinary(m.Groups[1].Value, depth + 1);
        return "";
    }

    // Go's RE2 patterns, spelled for .NET: Go's \s is [\t\n\f\r ] and \S its
    // opposite, and \b is an ASCII word boundary; .NET's are Unicode.
    const string S = @"[\t\n\f\r ]";
    const string NS = @"[^\t\n\f\r ]";

    [GeneratedRegex("--app=|launch-webapp", RegexOptions.CultureInvariant)]
    private static partial Regex WebAppRun();

    [GeneratedRegex("(?m)^" + S + "*(exec" + S + "+)?" + NS + "*/?electron[0-9]*" + S, RegexOptions.CultureInvariant)]
    private static partial Regex ElectronRun();

    [GeneratedRegex(@"require_version\(" + S + "*['\"](Gtk['\"]" + S + "*," + S + "*['\"]4|Adw['\"])", RegexOptions.CultureInvariant)]
    private static partial Regex Gtk4Import();

    [GeneratedRegex(@"from gi\.repository import[^\n]*(?<![0-9A-Za-z_])Gtk(?![0-9A-Za-z_])|require_version\(" + S + "*['\"]Gtk", RegexOptions.CultureInvariant)]
    private static partial Regex GtkImport();

    [GeneratedRegex("(?m)^" + S + "*(from|import)" + S + "+(PyQt[56]|PySide[26])", RegexOptions.CultureInvariant)]
    private static partial Regex QtImport();

    [GeneratedRegex("(?m)^" + S + "*exec" + S + "+\"?(/[^\\t\\n\\f\\r \"]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ExecTarget();

    /// <summary>
    /// FromLibs: the toolkit's own libraries, or ones built on it
    /// (libKF6…, libFcitx5Qt6…).
    /// </summary>
    internal static string FromLibs(string s)
    {
        if (s.Contains("libgtk-4") || s.Contains("libadwaita")) return Toolkit.GTK4;
        if (s.Contains("libgtk-3")) return Toolkit.GTK3;
        if (s.Contains("Qt6") || s.Contains("libKF6")) return Toolkit.Qt6;
        if (s.Contains("Qt5") || s.Contains("libKF5")) return Toolkit.Qt5;
        return "";
    }

    /// <summary>
    /// ResolvePackages decides, from what their pacman packages depend on, the
    /// toolkit of the apps their binary didn't tell: two pacman calls in all.
    /// </summary>
    internal void ResolvePackages(List<App> apps)
    {
        var paths = ByPath.Where(kv => kv.Value == "").Select(kv => kv.Key).ToList();
        if (paths.Count == 0) return;
        paths.Sort(GoStrings.Comparer);
        var owner = new Dictionary<string, string>(); // path -> package
        foreach (var line in RunPacman(["-Qo", .. paths]).Split('\n'))
        {
            // "/usr/bin/x is owned by pkg 1.0-1"
            var i = line.IndexOf(" is owned by ", StringComparison.Ordinal);
            if (i < 0) continue;
            var f = Pacman.Exec.Fields(line[(i + " is owned by ".Length)..]);
            if (f.Length > 0) owner[line[..i]] = f[0];
        }
        // Go ranges over a map here: the order was random; sorted is one of them.
        var pkgs = owner.Values.Distinct().Order(GoStrings.Comparer).ToList();
        var deps = new Dictionary<string, string>(); // package -> toolkit
        if (pkgs.Count > 0)
        {
            var name = "";
            foreach (var line in RunPacman(["-Qi", .. pkgs]).Split('\n'))
            {
                var c = line.IndexOf(':');
                var (k, v) = c < 0 ? (line, "") : (line[..c], line[(c + 1)..]);
                switch (k.Trim())
                {
                    case "Name":
                        name = v.Trim();
                        break;
                    case "Depends On":
                        deps[name] = FromDeps(Pacman.Exec.Fields(v));
                        break;
                }
            }
        }
        foreach (var p in ByPath.Keys.ToList())
        {
            if (ByPath[p] != "") continue;
            ByPath[p] = Toolkit.Other;
            if (deps.GetValueOrDefault(owner.GetValueOrDefault(p, ""), "") is { Length: > 0 } t) ByPath[p] = t;
        }
        foreach (var a in apps)
        {
            if (a.Toolkit != "") continue;
            var path = Exec.LookPath(a.Exec) ?? "";
            path = Paths.Real(path) ?? path;
            a.Toolkit = ByPath.GetValueOrDefault(path, "");
            if (a.Toolkit == "") a.Toolkit = Toolkit.Other;
        }
    }

    /// <summary>
    /// RunPacman runs pacman in English: the field names it prints ("Depends
    /// On") are translated otherwise. Its output even when it fails (-Qo fails
    /// when one path has no owner, and still names the others').
    /// </summary>
    static string RunPacman(string[] args) => Exec.Run("pacman", args, ("LC_ALL", "C")).Stdout;

    /// <summary>FromDeps: web engines before GTK (they depend on gtk3 for dialogs).</summary>
    internal static string FromDeps(IReadOnlyList<string> deps)
    {
        bool Has(params string[] prefixes)
        {
            foreach (var d in deps)
            {
                var name = d.Split('>', 2)[0]; // "gtk3>=3.24"
                name = name.Split('=', 2)[0];
                foreach (var p in prefixes)
                    if (name == p || p.StartsWith("electron", StringComparison.Ordinal) && name.StartsWith("electron", StringComparison.Ordinal))
                        return true;
            }
            return false;
        }
        if (Has("electron")) return Toolkit.Electron;
        if (Has("nss") && Has("gtk3")) return Toolkit.Chromium; // Chromium and CEF builds: nss + gtk3
        if (Has("webkit2gtk", "webkit2gtk-4.1", "webkitgtk-6.0", "flutter")) return Toolkit.Other;
        if (Has("gtk4", "libadwaita")) return Toolkit.GTK4;
        if (Has("gtk3")) return Toolkit.GTK3;
        if (Has("qt6-base")) return Toolkit.Qt6;
        if (Has("qt5-base")) return Toolkit.Qt5;
        return "";
    }
}
