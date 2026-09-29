using System.Text.RegularExpressions;
using MyArch.Util;

namespace MyArch.Store;

/// <summary>
/// An app the Apps menu (and the installer's profiles) knows how to install:
/// its packages from Arch's official repositories, or its Flatpak, or a site
/// as an app of its own (webapps), or a myarch plugin; with the myarch
/// plugins that go with it.
/// </summary>
public sealed class App
{
    public string Id = "", Name = "", Description = "", Category = "", Desktop = "";
    public List<string> Pacman = [];
    public string Flatpak = "", Webapp = "", Plugin = "";
    public List<string> Plugins = [];
    public Dictionary<string, (string Name, string Description)> Translations = [];

    /// <summary>How it's installed: pacman, flatpak, webapp or plugin.</summary>
    public string Kind => Pacman.Count > 0 ? "pacman" : Flatpak != "" ? "flatpak" : Webapp != "" ? "webapp" : "plugin";

    public (string Name, string Description) In(string lang)
    {
        foreach (var l in new[] { lang, lang.Split('_')[0] })
            if (Translations.TryGetValue(l, out var t))
                return (t.Name != "" ? t.Name : Name, t.Description != "" ? t.Description : Description);
        return (Name, Description);
    }
}

/// <summary>A set of apps for a use ("Gaming"), offered as one card.</summary>
public sealed class Profile
{
    public string Id = "", Name = "", Description = "";
    public long Glyph;
    /// <summary>What every computer gets (the installer's, preselected).</summary>
    public bool Basic;
    public List<string> Apps = [];
    public Dictionary<string, (string Name, string Description)> Translations = [];

    public (string Name, string Description) In(string lang)
    {
        foreach (var l in new[] { lang, lang.Split('_')[0] })
            if (Translations.TryGetValue(l, out var t))
                return (t.Name != "" ? t.Name : Name, t.Description != "" ? t.Description : Description);
        return (Name, Description);
    }
}

/// <summary>
/// AppCatalog reads catalog/apps.toml: every entry checked, as its names end
/// up in commands run as root (pacman) or with the network (flatpak).
/// </summary>
public static partial class AppCatalog
{
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*\z")] private static partial Regex IdPattern();
    // Arch package names: letters, digits, @ . _ + -, not starting with - or .
    [GeneratedRegex(@"^[a-z0-9@_+][a-z0-9@._+-]*\z")] private static partial Regex PackagePattern();
    // Flatpak application ids: reverse DNS.
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_-]*(\.[A-Za-z0-9_-]+){2,}\z")] private static partial Regex FlatpakPattern();
    [GeneratedRegex(@"^[a-z]{2,3}(_[A-Z]{2})?\z")] private static partial Regex LangPattern();
    [GeneratedRegex(@"^[A-Za-z0-9._@+-]+\.desktop\z")] private static partial Regex DesktopPattern();

    public static bool IsPackage(string s) => PackagePattern().IsMatch(s);
    public static bool IsFlatpak(string s) => FlatpakPattern().IsMatch(s);
    public static bool IsWebapp(string s) => s.StartsWith("https://") && s.Length > 8 && !s.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\');

    public static readonly string[] Categories =
        ["internet", "communication", "office", "development", "graphics", "video", "audio", "games", "utilities", "finance"];

    public static (List<Profile> Profiles, List<App> Apps) Load(string path)
    {
        var text = File.ReadAllText(path);
        return Parse(text, path);
    }

    public static (List<Profile> Profiles, List<App> Apps) Parse(string text, string from)
    {
        var r = new TomlReader(Toml.Parse(text, from), from);
        var apps = new List<App>();
        foreach (var t in r.Array("app"))
        {
            var a = new App
            {
                Id = t.String("id"),
                Name = t.String("name"),
                Description = t.String("description"),
                Category = t.String("category"),
                Desktop = t.String("desktop"),
                Pacman = t.Strings("pacman"),
                Flatpak = t.String("flatpak"),
                Webapp = t.String("webapp"),
                Plugin = t.String("plugin"),
                Plugins = t.Strings("plugins"),
            };
            a.Translations = Translations(t, from, a.Id);
            t.Done();
            if (!IdPattern().IsMatch(a.Id)) throw new MyArchException($"{from}: app id \"{a.Id}\": lowercase letters, digits and dashes");
            if (a.Name == "") throw new MyArchException($"{from}: {a.Id}: a name");
            if (!Categories.Contains(a.Category)) throw new MyArchException($"{from}: {a.Id}: category \"{a.Category}\": one of {string.Join(", ", Categories)}");
            var sources = (a.Pacman.Count > 0 ? 1 : 0) + (a.Flatpak != "" ? 1 : 0) + (a.Webapp != "" ? 1 : 0) + (a.Plugin != "" ? 1 : 0);
            if (sources != 1) throw new MyArchException($"{from}: {a.Id}: exactly one of pacman, flatpak, webapp, plugin");
            if (a.Pacman.FirstOrDefault(p => !PackagePattern().IsMatch(p)) is { } bad) throw new MyArchException($"{from}: {a.Id}: package \"{bad}\"");
            if (a.Flatpak != "" && !FlatpakPattern().IsMatch(a.Flatpak)) throw new MyArchException($"{from}: {a.Id}: flatpak \"{a.Flatpak}\": an application id (org.example.App)");
            if (a.Webapp != "" && !IsWebapp(a.Webapp))
                throw new MyArchException($"{from}: {a.Id}: webapp \"{a.Webapp}\": an https address");
            if (a.Plugin != "" && !MyArch.Plugins.Plugin.IdPattern().IsMatch(a.Plugin)) throw new MyArchException($"{from}: {a.Id}: plugin \"{a.Plugin}\"");
            if (a.Plugins.FirstOrDefault(p => !MyArch.Plugins.Plugin.IdPattern().IsMatch(p)) is { } badp) throw new MyArchException($"{from}: {a.Id}: plugins: \"{badp}\"");
            if (a.Desktop != "" && !DesktopPattern().IsMatch(a.Desktop)) throw new MyArchException($"{from}: {a.Id}: desktop \"{a.Desktop}\"");
            if (apps.Any(x => x.Id == a.Id)) throw new MyArchException($"{from}: app {a.Id} twice");
            apps.Add(a);
        }
        var profiles = new List<Profile>();
        foreach (var t in r.Array("profile"))
        {
            var p = new Profile
            {
                Id = t.String("id"),
                Name = t.String("name"),
                Description = t.String("description"),
                Glyph = t.Int("glyph"),
                Basic = t.Bool("basic"),
                Apps = t.Strings("apps"),
            };
            p.Translations = Translations(t, from, p.Id);
            t.Done();
            if (!IdPattern().IsMatch(p.Id)) throw new MyArchException($"{from}: profile id \"{p.Id}\"");
            if (p.Name == "") throw new MyArchException($"{from}: profile {p.Id}: a name");
            // One name, one thing: an id is an app's or a profile's.
            if (apps.Any(a => a.Id == p.Id)) throw new MyArchException($"{from}: profile {p.Id}: an app has that id too");
            if (p.Basic && profiles.Any(x => x.Basic)) throw new MyArchException($"{from}: profile {p.Id}: only one is basic");
            if (p.Apps.FirstOrDefault(id => apps.All(a => a.Id != id)) is { } missing)
                throw new MyArchException($"{from}: profile {p.Id}: no app \"{missing}\"");
            if (profiles.Any(x => x.Id == p.Id)) throw new MyArchException($"{from}: profile {p.Id} twice");
            profiles.Add(p);
        }
        r.Done();
        return (profiles, apps);
    }

    static Dictionary<string, (string, string)> Translations(TomlReader t, string from, string id)
    {
        var out_ = new Dictionary<string, (string, string)>();
        if (!t.Has("translations")) return out_;
        var tr = t.Sub("translations");
        foreach (var lang in tr.Table.Keys.ToList())
        {
            if (!LangPattern().IsMatch(lang)) throw new MyArchException($"{from}: {id}: translations.{lang}: a language code (es, pt_BR)");
            var l = tr.Sub(lang);
            out_[lang] = (l.String("name"), l.String("description"));
        }
        return out_;
    }
}
