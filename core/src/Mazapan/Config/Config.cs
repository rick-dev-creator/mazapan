using System.Text;
using Mazapan.Util;
using Tomlyn.Model;

namespace Mazapan.Config;

/// <summary>
/// Reads and writes ~/.config/mazapan/config.toml, the only file a person
/// edits by hand.
/// </summary>
public sealed class Settings
{
    public string Theme = "";
    /// <summary>
    /// Accent color (#rrggbb) instead of the theme's; its other accent tokens
    /// are derived from it. Empty = the theme's own.
    /// </summary>
    public string Accent = "";
    /// <summary>Language to render text in ("es", "es_MX"); empty = the OS's.</summary>
    public string Language = "";
    /// <summary>Fonts instead of the theme's (Settings › Text): empty, and 0, the theme's own.</summary>
    public string FontUI = "", FontMono = "";
    public double FontSize;
    public List<string> Disabled = [];
    /// <summary>Catalogs of plugins to add besides mazapan's own: URLs or paths of index.toml files.</summary>
    public List<string> Catalogs = [];
    /// <summary>Catalogs of apps besides mazapan's own (the Apps menu): URLs or paths of apps.toml files.</summary>
    public List<string> AppCatalogs = [];
    /// <summary>Hardware plugins are off until turned on: the ones that are.</summary>
    public List<string> Enabled = [];
    /// <summary>Per-plugin setting overrides: [plugins.&lt;id&gt;] key = value</summary>
    public SortedDictionary<string, SortedDictionary<string, object>> Plugins = new(StringComparer.Ordinal);

    public static string Path => Paths.ExpandHome("~/.config/mazapan/config.toml");

    public static Settings Load() => LoadFrom(Path);

    /// <summary>LoadFrom reads a config.toml elsewhere (a snapshot's copy).</summary>
    public static Settings LoadFrom(string path)
    {
        var c = new Settings();
        var t = Toml.ReadFile(path);
        if (t == null) return c;
        var r = new TomlReader(t, path);
        c.Theme = r.String("theme");
        c.Accent = r.String("accent");
        c.Language = r.String("language");
        c.FontUI = r.String("font_ui");
        c.FontMono = r.String("font_mono");
        c.FontSize = r.Float("font_size");
        c.Disabled = r.Strings("disabled_plugins");
        c.Enabled = r.Strings("enabled_plugins");
        c.Catalogs = r.Strings("catalogs");
        c.AppCatalogs = r.Strings("app_catalogs");
        if (r.Raw("plugins") is { } plugins)
            foreach (var (id, v) in plugins)
            {
                if (v is not TomlTable table) throw new MazapanException($"{path}: [plugins.{id}] must be a table");
                var m = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var (k, x) in table) m[k] = x;
                c.Plugins[id] = m;
            }
        r.Done();
        return c;
    }

    public void Save() =>
        // Atomically, and through a symlink (dotfiles kept with stow or chezmoi).
        Files.WriteAtomic(Paths.Real(Path) ?? Path, Text());

    /// <summary>The file's text, as Save writes it.</summary>
    public string Text()
    {
        var b = new StringBuilder("# mazapan configuration. Apply changes with: mazapan apply\n\n");
        TomlWriter.Key(b, "theme", Theme);
        if (Accent != "") TomlWriter.Key(b, "accent", Accent);
        if (Language != "") TomlWriter.Key(b, "language", Language);
        if (FontUI != "") TomlWriter.Key(b, "font_ui", FontUI);
        if (FontMono != "") TomlWriter.Key(b, "font_mono", FontMono);
        if (FontSize > 0) TomlWriter.Key(b, "font_size", FontSize);
        if (Disabled.Count > 0) TomlWriter.Key(b, "disabled_plugins", Disabled);
        if (Enabled.Count > 0) TomlWriter.Key(b, "enabled_plugins", Enabled);
        if (Catalogs.Count > 0) TomlWriter.Key(b, "catalogs", Catalogs);
        if (AppCatalogs.Count > 0) TomlWriter.Key(b, "app_catalogs", AppCatalogs);
        if (Plugins.Count > 0)
        {
            b.Append("\n[plugins]\n");
            foreach (var (id, settings) in Plugins)
            {
                b.Append($"  [plugins.{TomlWriter.BareOrQuoted(id)}]\n");
                foreach (var (k, v) in settings) TomlWriter.Key(b, k, v, "    ");
            }
        }
        return b.ToString();
    }

    public bool IsDisabled(string id) => Disabled.Contains(id);

    /// <summary>
    /// IsOn: a plugin is on unless disabled; a hardware plugin is off unless
    /// enabled (it's for some machines, and can write system files), and so
    /// is an optional one (an extra, there for whoever wants it).
    /// </summary>
    public bool IsOn(Plugins.Plugin p) => p.OffByDefault ? Enabled.Contains(p.Id) : !Disabled.Contains(p.Id);

    public void TurnOn(Plugins.Plugin p)
    {
        Disabled.RemoveAll(d => d == p.Id);
        if (p.OffByDefault && !Enabled.Contains(p.Id)) Enabled.Add(p.Id);
    }

    public void TurnOff(Plugins.Plugin p)
    {
        Enabled.RemoveAll(d => d == p.Id);
        if (!p.OffByDefault && !Disabled.Contains(p.Id)) Disabled.Add(p.Id);
    }

    /// <summary>A plugin's overrides, or null.</summary>
    public IReadOnlyDictionary<string, object>? For(string id) => Plugins.GetValueOrDefault(id);
}

/// <summary>
/// Writes TOML the way BurntSushi/toml's encoder did, for the few files
/// mazapan writes (config.toml, plugins.lock).
/// </summary>
public static class TomlWriter
{
    public static string BareOrQuoted(string k) =>
        k.Length > 0 && k.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-') ? k : Str(k);

    public static string Str(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (c < 0x20 || c == 0x7f) b.Append($"\\u{(int)c:X4}");
                    else b.Append(c);
                    break;
            }
        }
        return b.Append('"').ToString();
    }

    public static string Value(object v) => v switch
    {
        string s => Str(s),
        bool x => x ? "true" : "false",
        long n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        int n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double d => FloatValue(d),
        IEnumerable<string> list => "[" + string.Join(", ", list.Select(Str)) + "]",
        IEnumerable<double> nums => "[" + string.Join(", ", nums.Select(FloatValue)) + "]",
        TomlArray a => "[" + string.Join(", ", a.Select(x => Value(x!))) + "]",
        TomlTable t => "{" + string.Join(", ", t.Select(kv => BareOrQuoted(kv.Key) + " = " + Value(kv.Value!))) + "}",
        TomlTableArray ta => "[" + string.Join(", ", ta.Select(t => Value(t))) + "]",
        Tomlyn.TomlDateTime dt => dt.ToString(),
        _ => Str(v.ToString() ?? ""),
    };

    /// <summary>A float as TOML reads it back: nan, inf, 1e+30, 2.0.</summary>
    static string FloatValue(double d)
    {
        if (double.IsNaN(d)) return "nan";
        if (double.IsInfinity(d)) return d > 0 ? "inf" : "-inf";
        var s = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture).Replace("E", "e");
        return s.Contains('.') || s.Contains('e') ? s : s + ".0";
    }

    public static void Key(StringBuilder b, string key, object value, string indent = "") =>
        b.Append(indent).Append(BareOrQuoted(key)).Append(" = ").Append(Value(value)).Append('\n');
}
