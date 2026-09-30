using System.Globalization;
using System.Text.RegularExpressions;
using Mazapan.Util;
using Tomlyn.Model;

namespace Mazapan.Plugins;

/// <summary>
/// What a setting is, besides its default: what the Plugins panel shows and
/// edits, and what's checked when it's set (config.toml, --set, an agent).
/// A plain setting (key = default) gets it from its default's type, its
/// name, and the comment above it in plugin.toml; [settings.key] with
/// default = … says more: label, description, choices, min, max, step,
/// kind. A plugin's locales can translate both: setting.KEY, setting.KEY.help.
/// </summary>
public sealed partial class SettingInfo
{
    public string Label = "", Description = "";
    /// <summary>text, number, integer, switch, choice, key (a key binding), command, color, path, font, list, table (edited in config.toml).</summary>
    public string Kind = "";
    public List<object> Choices = [];
    public double? Min, Max, Step;

    static readonly HashSet<string> Kinds = ["text", "number", "integer", "switch", "choice", "key", "command", "color", "path", "font", "list", "table"];

    /// <summary>The kind a default and a name suggest.</summary>
    public static string KindOf(string key, object def) => def switch
    {
        bool => "switch",
        long => "integer",
        double => "number",
        TomlArray => "list",
        TomlTable => "table",
        string when key == "key" || key.EndsWith("_key") => "key",
        string when key is "terminal" or "command" || key.EndsWith("_command") => "command",
        string => "text",
        _ => "text",
    };

    /// <summary>Humanized: font_size -> "Font size".</summary>
    public static string LabelOf(string key)
    {
        var s = key.Replace('_', ' ').Replace('-', ' ');
        return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>Reads [settings.key] = { default, label, description, choices, min, max, step, kind }.</summary>
    public static (object Default, SettingInfo Info) Parse(string path, string key, TomlTable t)
    {
        var r = new TomlReader(t, path, "settings." + key);
        if (!t.TryGetValue("default", out var def) || def == null)
            throw new MazapanException($"{path}: [settings.{key}] needs default");
        var info = new SettingInfo
        {
            Label = r.String("label"),
            Description = r.String("description"),
            Kind = r.String("kind"),
        };
        if (t.TryGetValue("choices", out var ch))
        {
            if (ch is not TomlArray arr || arr.Count == 0) throw new MazapanException($"{path}: [settings.{key}] choices: a list");
            info.Choices = arr.Select(x => x!).ToList();
        }
        if (t.ContainsKey("min")) info.Min = r.Float("min");
        if (t.ContainsKey("max")) info.Max = r.Float("max");
        if (t.ContainsKey("step")) info.Step = r.Float("step");
        foreach (var k in t.Keys)
            if (k is not ("default" or "label" or "description" or "kind" or "choices" or "min" or "max" or "step"))
                throw new MazapanException($"{path}: [settings.{key}] {k}: unknown (default, label, description, kind, choices, min, max, step)");
        if (info.Kind == "") info.Kind = info.Choices.Count > 0 ? "choice" : KindOf(key, def);
        if (!Kinds.Contains(info.Kind)) throw new MazapanException($"{path}: [settings.{key}] kind = \"{info.Kind}\": one of {string.Join(", ", Kinds)}");
        var fits = info.Kind switch
        {
            "switch" => def is bool,
            "integer" => def is long,
            "number" => def is long or double,
            "choice" => info.Choices.Count > 0,
            "list" => def is TomlArray,
            "table" => def is TomlTable,
            _ => def is string,
        };
        if (!fits)
            throw new MazapanException($"{path}: [settings.{key}] kind = \"{info.Kind}\" " +
                (info.Kind == "choice" ? "needs choices" : $"doesn't go with the default {Plugin.Show(def)}"));
        if (info.Step is <= 0) throw new MazapanException($"{path}: [settings.{key}] step: more than 0");
        if (info.Check(def) is { } why) throw new MazapanException($"{path}: [settings.{key}] default {why}");
        return (def, info);
    }

    /// <summary>Check says what's wrong with a value (null: nothing): not one of the choices, out of range.</summary>
    public string? Check(object v)
    {
        if (Choices.Count > 0 && !Choices.Any(c => Equals(c, v) || (c is long l && v is double d && l == d)))
            return $"= {Plugin.Show(v)}: one of {string.Join(", ", Choices.Select(c => c is string s ? GoFormat.Quote(s) : Plugin.Show(c)))}";
        var n = v switch { long l => (double?)l, double d => d, _ => null };
        if (n != null && Min != null && n < Min) return $"= {Plugin.Show(v)}: {GoFormat.Num(Min.Value)} at least";
        if (n != null && Max != null && n > Max) return $"= {Plugin.Show(v)}: {GoFormat.Num(Max.Value)} at most";
        return null;
    }

    /// <summary>
    /// Comments reads the comment right above each setting in plugin.toml
    /// (in [settings], or above a [settings.key] table): the description a
    /// plain setting has always had, for people reading the file.
    /// </summary>
    public static Dictionary<string, string> Comments(string text)
    {
        var out_ = new Dictionary<string, string>();
        var table = "";
        var buf = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#'))
            {
                buf.Add(line.TrimStart('#').Trim());
                continue;
            }
            if (line.StartsWith('['))
            {
                table = line.Trim('[', ']', ' ');
                if (table.StartsWith("settings.")) out_[table["settings.".Length..].Trim()] = Join(buf);
                buf.Clear();
                continue;
            }
            if (table == "settings" && KeyLine().Match(line) is { Success: true } m)
                out_[m.Groups[1].Value] = Join(buf);
            buf.Clear();
        }
        return out_;
    }

    static string Join(List<string> lines) => string.Join(" ", lines.Where(l => l != "")).Trim();

    [GeneratedRegex(@"^([A-Za-z0-9_-]+)\s*=")]
    private static partial Regex KeyLine();
}
