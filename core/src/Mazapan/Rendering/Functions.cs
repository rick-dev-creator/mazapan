using System.Globalization;
using System.Text;
using Mazapan.Locale;
using Mazapan.Themes;
using Mazapan.Util;
using Scriban.Runtime;

namespace Mazapan.Rendering;

/// <summary>The functions every template can call, besides Scriban's own.</summary>
static class Functions
{
    public static void Add(ScriptObject g, Theme t, List<string> outputs, Catalog cat)
    {
        // t "preview.empty" -> the plugin's text in the current language.
        g.Add("t", DelegateCustomFunction.CreateFunc((string key) => cat.T(key)));
        // tq "preview.empty" -> the same, as a quoted string literal that is
        // valid in QML/JS and Lua: "workspace %1 · vacío"
        g.Add("tq", DelegateCustomFunction.CreateFunc((string key) => GoFormat.Quote(cat.T(key))));
        // lq "SUPER + space" -> "\"SUPER + space\"": a string as a Lua literal,
        // so a setting can't close the string and add code.
        g.Add("lq", DelegateCustomFunction.CreateFunc((string s) => LuaQuote(s)));
        // inline "a\nb" -> "a b": for a comment or an ini value, where a line
        // break would start something else.
        g.Add("inline", DelegateCustomFunction.CreateFunc((string s) =>
            new string(s.Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray())));
        // quote "text" -> "\"text\"": any string as such a literal.
        g.Add("quote", DelegateCustomFunction.CreateFunc((string s) => GoFormat.Quote(s)));
        // shq "it's $HOME" -> "'it'\''s $HOME'": one word in a shell command,
        // nothing in it expanded ($, `, globs): for settings in commands.
        g.Add("shq", DelegateCustomFunction.CreateFunc((string s) => ShellQuote(s)));
        // c "accent" -> "#ffb000"; fails the render on unknown tokens.
        g.Add("c", DelegateCustomFunction.CreateFunc((string name) => t.Color(name)));
        // hex "#ffb000" -> "ffb000"
        g.Add("hex", DelegateCustomFunction.CreateFunc((string c) => c.StartsWith('#') ? c[1..] : c));
        // rgb "#ffb000" -> "rgb(ffb000)"  (Hyprland color syntax)
        g.Add("rgb", DelegateCustomFunction.CreateFunc((string c) => "rgb(" + Six(c) + ")"));
        // rgba "#ffb000" 0.5 -> "rgba(ffb00080)"  (Hyprland color syntax)
        g.Add("rgba", DelegateCustomFunction.CreateFunc((string c, double a) => $"rgba({Six(c)}{AlphaByte(a):x2})"));
        // cssa "#ffb000" 0.5 -> "rgba(255, 176, 0, 0.5)"  (CSS syntax)
        g.Add("cssa", DelegateCustomFunction.CreateFunc((string c, double a) =>
        {
            var (r, gr, b) = Channels(c);
            return $"rgba({r}, {gr}, {b}, {GoFormat.Num(a)})";
        }));
        // speed 0.6 -> Hyprland animation speed (deciseconds) for 60% of the
        // theme's base duration.
        g.Add("speed", DelegateCustomFunction.CreateFunc((double factor) =>
            GoFormat.Fixed(t.Motion.DurationMS * factor / 100, 2)));
        // under "~/.config/hypr/mazapan/" -> sorted outputs of all plugins
        // below that directory. Lets an entry-point file include fragments
        // without knowing which plugins exist.
        g.Add("under", DelegateCustomFunction.CreateFunc((string prefix) =>
        {
            prefix = Paths.ExpandHome(prefix);
            var list = new ScriptArray();
            foreach (var o in outputs)
                if (o.StartsWith(prefix, StringComparison.Ordinal)) list.Add(o);
            return list;
        }));
        // csv "#ffb000" -> "255,176,0"  (KDE color syntax)
        g.Add("csv", DelegateCustomFunction.CreateFunc((string c) =>
        {
            var (r, gr, b) = Channels(c);
            return $"{r},{gr},{b}";
        }));
        // spring 1.2 0.9 -> Hyprland spring curve fields for the theme's
        // spring with its response scaled by 1.2 and damping 0.9 (0 keeps
        // the theme's damping). A mass-spring with period T and damping
        // ratio z has stiffness (2*pi/T)^2 and dampening 2*z*(2*pi/T).
        g.Add("spring", DelegateCustomFunction.CreateFunc((double responseFactor, double damping) =>
        {
            if (damping == 0) damping = t.Motion.Damping;
            var omega = 2 * Math.PI / (t.Motion.ResponseMS * responseFactor / 1000);
            return $"mass = 1, stiffness = {GoFormat.Fixed(omega * omega, 2)}, dampening = {GoFormat.Fixed(2 * damping * omega, 2)}";
        }));
        // num 1.5 -> "1.5": a number as it's written, however it was stored.
        g.Add("num", DelegateCustomFunction.CreateFunc((double f) => GoFormat.Num(f)));
        // pct 0.9 -> "90"
        g.Add("pct", DelegateCustomFunction.CreateFunc((double f) =>
            ((long)GoFormat.Round(f * 100)).ToString(CultureInfo.InvariantCulture)));
        // json actions -> a JSON value, which is also a valid QML/JS literal:
        // data for a template's code.
        g.Add("json", DelegateCustomFunction.CreateFunc((object v) => Json(v)));
        // base "/x/y.default" -> "y.default" (a profile's name, from place)
        g.Add("base", DelegateCustomFunction.CreateFunc((string p) => Paths.Base(p)));
        // mix (c "bg") (c "success") 0.18 -> the second over the first at
        // 18%: "#2a3322" (a diff's added line)
        g.Add("mix", DelegateCustomFunction.CreateFunc((string a, string b, double f) =>
        {
            var (ar, ag, ab) = Channels(a);
            var (br, bg, bb) = Channels(b);
            int M(int x, int y) => (int)GoFormat.Round(x + (y - x) * f);
            return $"#{M(ar, br):x2}{M(ag, bg):x2}{M(ab, bb):x2}";
        }));
        // solid "#ffb00080" -> "#ffb000" (for apps with no alpha: Neovim, btop)
        g.Add("solid", DelegateCustomFunction.CreateFunc((string c) => c.Length > 7 ? c[..7] : c));
        // camel "bg_alt" -> "bgAlt"  (QML/JS property names)
        g.Add("camel", DelegateCustomFunction.CreateFunc((string s) =>
        {
            var parts = s.Split('_');
            for (var i = 1; i < parts.Length; i++)
                if (parts[i] != "") parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
            return string.Concat(parts);
        }));
    }

    static string LuaQuote(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (ch < 0x20 || ch == 0x7f) b.Append('\\').Append(((int)ch).ToString("000", CultureInfo.InvariantCulture));
                    else b.Append(ch);
                    break;
            }
        }
        return b.Append('"').ToString();
    }

    static string Six(string c)
    {
        var h = c.StartsWith('#') ? c[1..] : c;
        if (h.Length < 6) throw new MazapanException($"bad color \"{c}\"");
        return h[..6];
    }

    static int AlphaByte(double a) => a <= 0 ? 0 : a >= 1 ? 255 : (int)(a * 255 + 0.5);

    static (int, int, int) Channels(string c)
    {
        var h = c.StartsWith('#') ? c[1..] : c;
        if (h.Length < 6 || !uint.TryParse(h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new MazapanException($"bad color \"{c}\"");
        return ((int)(v >> 16 & 0xff), (int)(v >> 8 & 0xff), (int)(v & 0xff));
    }

    /// <summary>JSON as Go's encoding/json writes it: objects' keys in order, HTML-safe strings.</summary>
    public static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    public static string Json(object? v)
    {
        var b = new StringBuilder();
        Write(b, v, 0);
        return b.ToString();
    }

    static void Write(StringBuilder b, object? v, int depth)
    {
        // An object that holds itself would never end.
        if (depth > 64) throw new MazapanException("json: nested more than 64 deep (does it hold itself?)");
        switch (v)
        {
            case null:
                b.Append("null");
                break;
            case string s:
                GoFormat.JsonString(b, s);
                break;
            case bool x:
                b.Append(x ? "true" : "false");
                break;
            case long or int:
                b.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                break;
            case double d:
                b.Append(GoFormat.Float(d));
                break;
            case ScriptObject o:
                b.Append('{');
                var first = true;
                foreach (var k in o.Keys)
                {
                    if (!first) b.Append(',');
                    first = false;
                    GoFormat.JsonString(b, k);
                    b.Append(':');
                    Write(b, o[k], depth + 1);
                }
                b.Append('}');
                break;
            case System.Collections.IEnumerable list:
                b.Append('[');
                var i = 0;
                foreach (var x in list)
                {
                    if (i++ > 0) b.Append(',');
                    Write(b, x, depth + 1);
                }
                b.Append(']');
                break;
            default:
                GoFormat.JsonString(b, v.ToString() ?? "");
                break;
        }
    }
}

/// <summary>
/// What templates see, as Scriban objects (no reflection: Native AOT), named
/// as templates write them: theme.meta.mode, settings.key, lang_code.
/// </summary>
static class Model
{
    static ScriptObject Frozen(ScriptObject o)
    {
        o.IsReadOnly = true;
        return o;
    }

    static ScriptArray Frozen(ScriptArray a)
    {
        a.IsReadOnly = true;
        return a;
    }

    /// <summary>A map as an object with its keys sorted (Go ranged over maps that way).</summary>
    static ScriptObject Sorted(Dictionary<string, string> m)
    {
        var o = new ScriptObject();
        foreach (var k in m.Keys.Order(StringComparer.Ordinal)) o.Add(k, m[k]);
        return Frozen(o);
    }

    public static ScriptObject Theme(Theme t)
    {
        var accents = Frozen(new ScriptArray(t.Meta.Accents));
        var curve = Frozen(new ScriptArray(t.Motion.Curve.Cast<object>()));
        return Frozen(new ScriptObject
        {
            ["id"] = t.Id,
            ["meta"] = Frozen(new ScriptObject
            {
                ["name"] = t.Meta.Name,
                ["mode"] = t.Meta.Mode,
                ["description"] = t.Meta.Description,
                ["accents"] = accents,
            }),
            ["colors"] = Sorted(t.Colors),
            ["ansi"] = Sorted(t.Ansi),
            ["font"] = Frozen(new ScriptObject { ["mono"] = t.Font.Mono, ["ui"] = t.Font.UI, ["size"] = t.Font.Size }),
            ["shape"] = Frozen(new ScriptObject
            {
                ["radius"] = t.Shape.Radius,
                ["border"] = t.Shape.Border,
                ["gap_in"] = t.Shape.GapIn,
                ["gap_out"] = t.Shape.GapOut,
            }),
            ["effects"] = Frozen(new ScriptObject
            {
                ["terminal_opacity"] = t.Effects.TerminalOpacity,
                ["blur"] = t.Effects.Blur,
                ["wallpaper"] = t.Effects.Wallpaper,
            }),
            ["motion"] = Frozen(new ScriptObject
            {
                ["enabled"] = t.Motion.Enabled,
                ["duration_ms"] = t.Motion.DurationMS,
                ["curve"] = curve,
                ["response_ms"] = t.Motion.ResponseMS,
                ["damping"] = t.Motion.Damping,
            }),
        });
    }

    /// <summary>The machine, for hardware plugins: maker, model, and each GPU's name and driver.</summary>
    public static ScriptObject Machine(Hardware.Machine m)
    {
        var gpus = new ScriptArray(m.Gpus.Select(g => (object)Frozen(new ScriptObject
        {
            ["id"] = $"{g.Vendor}:{g.Device}",
            ["name"] = g.Name,
            ["driver"] = g.Driver,
        })));
        return Frozen(new ScriptObject
        {
            ["vendor"] = m.Vendor,
            ["product"] = m.Product,
            ["gpus"] = Frozen(gpus),
        });
    }

    public static ScriptObject Settings(Dictionary<string, object> settings)
    {
        var o = new ScriptObject();
        foreach (var k in settings.Keys.Order(StringComparer.Ordinal)) o.Add(k, Value(settings[k]));
        return Frozen(o);
    }

    static object Value(object v)
    {
        switch (v)
        {
            case Tomlyn.Model.TomlArray a:
                return Frozen(new ScriptArray(a.Select(x => Value(x!))));
            case Tomlyn.Model.TomlTable t:
                var o = new ScriptObject();
                foreach (var k in t.Keys.Order(StringComparer.Ordinal)) o.Add(k, Value(t[k]));
                return Frozen(o);
            default:
                return v;
        }
    }

    /// <summary>
    /// Every plugin's actions, rendered: what a command palette lists. Empty
    /// fields are left out, as the JSON the palette reads always did.
    /// </summary>
    public static ScriptArray Actions(IEnumerable<RenderedAction> actions)
    {
        var list = new ScriptArray();
        foreach (var a in actions)
        {
            var o = new ScriptObject { ["plugin"] = a.Plugin, ["name"] = a.Name };
            if (a.Run != "") o["run"] = a.Run;
            if (a.Key != "") o["key"] = a.Key;
            if (a.Terminal) o["terminal"] = true;
            if (a.Keywords != "") o["keywords"] = a.Keywords;
            list.Add(Frozen(o));
        }
        return Frozen(list);
    }
}
