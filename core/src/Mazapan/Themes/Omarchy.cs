using Mazapan.Util;
using Tomlyn.Model;

namespace Mazapan.Themes;

/// <summary>
/// An Omarchy theme made a Mazapan one: its palette (colors.toml, or its
/// alacritty.toml where an older theme has no colors.toml) mapped to
/// Mazapan's tokens, the accent's own tokens worked out so every contrast
/// the desktop promises holds, the status colors moved just enough to read,
/// and its first background as the wallpaper.
/// </summary>
public static class Omarchy
{
    /// <summary>Its palette, by Omarchy's names (background, foreground, accent, red…), from colors.toml or alacritty.toml; null when it has neither.</summary>
    /// <summary>A plain file (not a link: a repository's link could point at anything of the person's).</summary>
    static bool Plain(string path) => File.Exists(path) && new FileInfo(path).LinkTarget == null;

    public static Dictionary<string, string>? Palette(string dir)
    {
        var colors = Path.Join(dir, "colors.toml");
        if (Plain(colors))
        {
            var out_ = new Dictionary<string, string>();
            foreach (var (k, v) in Toml.Parse(File.ReadAllText(colors), colors))
                if (v is string s) out_[k] = s;
            return out_;
        }
        var alacritty = Path.Join(dir, "alacritty.toml");
        if (!Plain(alacritty)) return null;
        var t = Toml.Parse(File.ReadAllText(alacritty), alacritty);
        var p = new Dictionary<string, string>();
        TomlTable? Sub(TomlTable? x, string k) => x != null && x.TryGetValue(k, out var v) ? v as TomlTable : null;
        var c = Sub(t, "colors");
        void Take(TomlTable? from, string key, string name)
        {
            if (from != null && from.TryGetValue(key, out var v) && v is string s) p[name] = s.Replace("0x", "#", StringComparison.Ordinal);
        }
        Take(Sub(c, "primary"), "background", "background");
        Take(Sub(c, "primary"), "foreground", "foreground");
        Take(Sub(c, "selection"), "background", "selection");
        foreach (var n in new[] { "red", "green", "yellow", "blue", "magenta", "cyan" })
        {
            Take(Sub(c, "normal"), n, n);
            Take(Sub(c, "bright"), n, "bright_" + n);
        }
        Take(Sub(c, "bright"), "black", "muted");
        Take(Sub(c, "bright"), "white", "bright_foreground");
        return p.ContainsKey("background") && p.ContainsKey("foreground") ? p : null;
    }

    /// <summary>Mazapan's colors, ansi and suggested accents for that palette (and its mode: Omarchy's, or as its background is).</summary>
    public static (Dictionary<string, string> Colors, Dictionary<string, string> Ansi, List<string> Accents, string Mode) Convert(Dictionary<string, string> o)
    {
        Rgb Get(string k) => Rgb.Parse(o[k]);
        Rgb? Maybe(string k) => o.TryGetValue(k, out var v) && TryParse(v) is { } c ? c : null;
        var bg = Get("background");
        var fg = Get("foreground");
        var mode = o.GetValueOrDefault("mode") is "light" or "dark" ? o["mode"] : bg.Luminance() > 0.4 ? "light" : "dark";
        var light = mode == "light";
        static Rgb Mix(Rgb a, Rgb b, double t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        var raised = Maybe("lighter_background") ?? Mix(bg, fg, 0.10);
        var subtle = Maybe("dark_foreground") ?? Maybe("muted") ?? Mix(fg, bg, 0.5);
        var colors = new Dictionary<string, string>
        {
            ["bg"] = bg.Hex(),
            ["bg_alt"] = (Maybe("dark_background") ?? Mix(bg, new Rgb(0, 0, 0), 0.12)).Hex(),
            ["surface"] = Mix(bg, raised, 0.5).Hex(),
            ["surface_raised"] = raised.Hex(),
            // Between its raised surface and its muted text, just visible on the background.
            ["border"] = ColorMath.Against(Mix(raised, Maybe("muted") ?? subtle, 0.5), [bg], 1.3, light).Hex(),
            ["fg"] = fg.Hex(),
        };
        // Text tones: Omarchy's, moved only as far as reading them on the background needs.
        colors["fg_muted"] = ColorMath.Against(Maybe("light_foreground") ?? Mix(fg, bg, 0.25), [bg, raised], 4.5, light).Hex();
        colors["fg_subtle"] = ColorMath.Against(subtle, [bg], 3, light).Hex();
        var accent = Maybe("accent") ?? Maybe("blue") ?? fg;
        foreach (var (k, v) in ColorMath.AccentTokens(accent.Hex(), colors, mode)) colors[k] = v;
        // Omarchy's selection where text still reads on it.
        if (Maybe("selection") is { } sel && ColorMath.Contrast(fg.Hex(), sel.Hex()) >= 4.5) colors["selection"] = sel.Hex();
        string Status(string name, string fallback) => ColorMath.Against(Maybe(name) ?? Rgb.Parse(fallback), [bg], 4.5, light).Hex();
        colors["success"] = Status("green", "#4caf50");
        colors["warning"] = Status(o.ContainsKey("yellow") ? "yellow" : "orange", "#e0a000");
        colors["danger"] = Status("red", "#e05050");
        colors["info"] = Status("cyan", "#40a0c0");

        // The terminal as Omarchy draws it.
        string A(string k, string fallback) => (Maybe(k) ?? Rgb.Parse(fallback)).Hex();
        var ansi = new Dictionary<string, string>
        {
            ["black"] = bg.Hex(), ["red"] = A("red", "#cc3333"), ["green"] = A("green", "#33aa33"), ["yellow"] = A("yellow", "#ccaa33"),
            ["blue"] = A("blue", "#3366cc"), ["magenta"] = A("magenta", "#aa33aa"), ["cyan"] = A("cyan", "#33aaaa"), ["white"] = fg.Hex(),
        };
        foreach (var n in new[] { "red", "green", "yellow", "blue", "magenta", "cyan" }) ansi["bright_" + n] = (Maybe("bright_" + n) ?? Rgb.Parse(ansi[n])).Hex();
        ansi["bright_black"] = (Maybe("muted") ?? subtle).Hex();
        ansi["bright_white"] = (Maybe("bright_foreground") ?? fg).Hex();

        // Accents to offer: the theme's own first, then its colors that read as one.
        var accents = new List<string> { colors["accent"] };
        foreach (var n in new[] { "blue", "magenta", "green", "yellow", "cyan", "orange", "red" })
            if (Maybe(n) is { } c && ColorMath.Contrast(c.Hex(), bg.Hex()) >= 3 && !accents.Contains(c.Hex()) && accents.Count < 5) accents.Add(c.Hex());
        return (colors, ansi, accents, mode);
    }

    static Rgb? TryParse(string s)
    {
        try { return Rgb.Parse(s.Trim()); }
        catch (FormatException) { return null; }
    }

    /// <summary>Its first background (sorted, as Omarchy shows them), or null.</summary>
    public static string? Wallpaper(string dir)
    {
        var bgs = Path.Join(dir, "backgrounds");
        if (!Directory.Exists(bgs) || new DirectoryInfo(bgs).LinkTarget != null) return null;
        return Directory.GetFiles(bgs).Where(f => Plain(f) && Path.GetExtension(f).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp")
            .Order(StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>"tokyo-night" → "Tokyo Night".</summary>
    public static string Readable(string name) =>
        string.Join(' ', name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>Its id here: "omarchy-" and its folder's name as an id.</summary>
    public static string Id(string name)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.StartsWith("omarchy-", StringComparison.Ordinal)) slug = slug["omarchy-".Length..];
        if (slug.EndsWith("-theme", StringComparison.Ordinal)) slug = slug[..^"-theme".Length];
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        return "omarchy-" + (slug == "" ? "theme" : slug);
    }
}
