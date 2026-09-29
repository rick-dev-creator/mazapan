using System.Text;
using MyArch.Util;
using Tomlyn.Model;

namespace MyArch.Config;

/// <summary>
/// Reads and writes ~/.config/myarch/config.toml, the only file a person
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
    public List<string> Disabled = [];
    /// <summary>Catalogs of plugins to add besides myarch's own: URLs or paths of index.toml files.</summary>
    public List<string> Catalogs = [];
    /// <summary>Hardware plugins are off until turned on: the ones that are.</summary>
    public List<string> Enabled = [];
    /// <summary>Per-plugin setting overrides: [plugins.&lt;id&gt;] key = value</summary>
    public SortedDictionary<string, SortedDictionary<string, object>> Plugins = new(StringComparer.Ordinal);

    public static string Path => Paths.ExpandHome("~/.config/myarch/config.toml");

    public static Settings Load()
    {
        var c = new Settings();
        var t = Toml.ReadFile(Path);
        if (t == null) return c;
        var r = new TomlReader(t, Path);
        c.Theme = r.String("theme");
        c.Accent = r.String("accent");
        c.Language = r.String("language");
        c.Disabled = r.Strings("disabled_plugins");
        c.Enabled = r.Strings("enabled_plugins");
        c.Catalogs = r.Strings("catalogs");
        if (r.Raw("plugins") is { } plugins)
            foreach (var (id, v) in plugins)
            {
                if (v is not TomlTable table) throw new MyArchException($"{Path}: [plugins.{id}] must be a table");
                var m = new SortedDictionary<string, object>(StringComparer.Ordinal);
                foreach (var (k, x) in table) m[k] = x;
                c.Plugins[id] = m;
            }
        r.Done();
        return c;
    }

    public void Save()
    {
        var b = new StringBuilder("# myarch configuration. Apply changes with: myarch apply\n\n");
        TomlWriter.Key(b, "theme", Theme);
        if (Accent != "") TomlWriter.Key(b, "accent", Accent);
        if (Language != "") TomlWriter.Key(b, "language", Language);
        if (Disabled.Count > 0) TomlWriter.Key(b, "disabled_plugins", Disabled);
        if (Enabled.Count > 0) TomlWriter.Key(b, "enabled_plugins", Enabled);
        if (Catalogs.Count > 0) TomlWriter.Key(b, "catalogs", Catalogs);
        if (Plugins.Count > 0)
        {
            b.Append("\n[plugins]\n");
            foreach (var (id, settings) in Plugins)
            {
                b.Append($"  [plugins.{TomlWriter.BareOrQuoted(id)}]\n");
                foreach (var (k, v) in settings) TomlWriter.Key(b, k, v, "    ");
            }
        }
        // Atomically, and through a symlink (dotfiles kept with stow or chezmoi).
        Files.WriteAtomic(Paths.Real(Path) ?? Path, b.ToString());
    }

    public bool IsDisabled(string id) => Disabled.Contains(id);

    /// <summary>
    /// IsOn: a plugin is on unless disabled; a hardware plugin is off unless
    /// enabled (it's for some machines, and can write system files).
    /// </summary>
    public bool IsOn(Plugins.Plugin p) => p.Hardware != null ? Enabled.Contains(p.Id) : !Disabled.Contains(p.Id);

    public void TurnOn(Plugins.Plugin p)
    {
        Disabled.RemoveAll(d => d == p.Id);
        if (p.Hardware != null && !Enabled.Contains(p.Id)) Enabled.Add(p.Id);
    }

    public void TurnOff(Plugins.Plugin p)
    {
        Enabled.RemoveAll(d => d == p.Id);
        if (p.Hardware == null && !Disabled.Contains(p.Id)) Disabled.Add(p.Id);
    }

    /// <summary>A plugin's overrides, or null.</summary>
    public IReadOnlyDictionary<string, object>? For(string id) => Plugins.GetValueOrDefault(id);
}

/// <summary>
/// Writes TOML the way BurntSushi/toml's encoder did, for the few files
/// myarch writes (config.toml, plugins.lock).
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
