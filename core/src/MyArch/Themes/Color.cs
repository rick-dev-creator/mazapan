using System.Globalization;

namespace MyArch.Themes;

/// <summary>Colors as sRGB channels in 0..1.</summary>
readonly record struct Rgb(double R, double G, double B)
{
    public static Rgb Parse(string s)
    {
        var h = s.StartsWith('#') ? s[1..] : s;
        if ((h.Length != 6 && h.Length != 8) ||
            !uint.TryParse(h[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            throw new FormatException($"bad color \"{s}\"");
        return new((v >> 16 & 0xff) / 255.0, (v >> 8 & 0xff) / 255.0, (v & 0xff) / 255.0);
    }

    public string Hex()
    {
        static int Ch(double x) => (int)Math.Round(Math.Max(0, Math.Min(1, x)) * 255, MidpointRounding.AwayFromZero);
        return $"#{Ch(R):x2}{Ch(G):x2}{Ch(B):x2}";
    }

    /// <summary>Luminance is WCAG's relative luminance.</summary>
    public double Luminance() => 0.2126 * ColorMath.Linear(R) + 0.7152 * ColorMath.Linear(G) + 0.0722 * ColorMath.Linear(B);

    public Lab OkLab()
    {
        double r = ColorMath.Linear(R), g = ColorMath.Linear(G), b = ColorMath.Linear(B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return new(
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }
}

/// <summary>OKLab: lightness that looks even, so a color can get lighter or darker without drifting in hue.</summary>
readonly record struct Lab(double L, double A, double B)
{
    public (Rgb, bool) ToRgb()
    {
        var l = Math.Pow(L + 0.3963377774 * A + 0.2158037573 * B, 3);
        var m = Math.Pow(L - 0.1055613458 * A - 0.0638541728 * B, 3);
        var s = Math.Pow(L - 0.0894841775 * A - 1.2914855480 * B, 3);
        var r = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var g = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var b = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        static bool In(double x) => x >= -1e-4 && x <= 1 + 1e-4;
        return (new Rgb(ColorMath.Gamma(Math.Max(0, r)), ColorMath.Gamma(Math.Max(0, g)), ColorMath.Gamma(Math.Max(0, b))),
            In(r) && In(g) && In(b));
    }
}

/// <summary>A contrast the theme promises.</summary>
public sealed record Pair(string Fg, string Bg, double Min, double Ratio = 0)
{
    public bool OK => Ratio >= Min;
}

public static class ColorMath
{
    internal static double Linear(double x) => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);

    internal static double Gamma(double x) => x <= 0.0031308 ? x * 12.92 : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;

    static double Ratio(Rgb a, Rgb b)
    {
        double la = a.Luminance(), lb = b.Luminance();
        if (la < lb) (la, lb) = (lb, la);
        return (la + 0.05) / (lb + 0.05);
    }

    /// <summary>Contrast is the WCAG contrast ratio of two #rrggbb colors (1 to 21).</summary>
    public static double Contrast(string a, string b) => Ratio(Rgb.Parse(a), Rgb.Parse(b));

    /// <summary>Mix goes from a (t = 0) to b (t = 1), in sRGB, like painting one over the other with opacity t.</summary>
    static Rgb Mix(Rgb a, Rgb b, double t) => new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    /// <summary>WithLightness keeps c's hue at lightness l, giving up chroma until the color exists on screen.</summary>
    static Rgb WithLightness(Rgb c, double l)
    {
        var o = c.OkLab();
        for (var k = 1.0; k >= 0; k -= 0.02)
        {
            var (out_, ok) = new Lab(l, o.A * k, o.B * k).ToRgb();
            if (ok) return out_;
        }
        return new Lab(l, 0, 0).ToRgb().Item1;
    }

    /// <summary>Rounded is c as it will be written (#rrggbb): contrast is promised for that, not for the exact value.</summary>
    static Rgb Rounded(Rgb c) => Rgb.Parse(c.Hex());

    /// <summary>
    /// Against moves c's lightness, as little as it takes, until it contrasts
    /// at least min with every one of bgs: lighter in a dark theme, darker in
    /// a light one.
    /// </summary>
    static Rgb Against(Rgb c, Rgb[] bgs, double min, bool light)
    {
        bool Ok(Rgb x) => bgs.All(b => Ratio(x, Rounded(b)) >= min);
        c = Rounded(c);
        if (Ok(c)) return c;
        var step = light ? -0.01 : 0.01;
        var l = c.OkLab().L;
        for (; l >= 0 && l <= 1; l += step)
        {
            var out_ = Rounded(WithLightness(c, l));
            if (Ok(out_)) return out_;
        }
        return Rounded(WithLightness(c, Math.Max(0, Math.Min(1, l))));
    }

    /// <summary>
    /// AccentTokens derives every accent token from one color, for a theme
    /// with these colors, so that every pair in Contrast() holds:
    ///   - accent: fills and borders, at least 3:1 on the background;
    ///   - accent_fg: text on it, black or white (one of them always has 4.5:1);
    ///   - accent_deep: large fills, near-black in the accent's hue (a pale
    ///     tint of it in a light theme);
    ///   - accent_text: the accent as text, 4.5:1 on the background, on cards
    ///     and on accent_deep;
    ///   - selection: the accent over the background, as strong as it can be
    ///     with text on it still readable.
    /// </summary>
    public static Dictionary<string, string> AccentTokens(string accent, Dictionary<string, string> colors, string mode)
    {
        var c = Rgb.Parse(accent);
        Rgb Token(string name)
        {
            try
            {
                return Rgb.Parse(colors.GetValueOrDefault(name, ""));
            }
            catch (FormatException e)
            {
                throw new FormatException($"{name}: {e.Message}");
            }
        }
        Rgb bg = Token("bg"), card = Token("surface_raised"), fg = Token("fg");
        var light = mode == "light";
        var fill = Against(c, [bg], 3, light);
        var onFill = new Rgb(1, 1, 1);
        if (Ratio(new Rgb(0, 0, 0), fill) > Ratio(onFill, fill)) onFill = new Rgb(0, 0, 0);
        var deep = Rounded(Mix(new Rgb(0, 0, 0), fill, 0.27));
        if (light) deep = Rounded(Mix(new Rgb(1, 1, 1), fill, 0.14));
        var text = Against(fill, [bg, card, deep], 4.5, light);
        var selection = Rounded(Mix(bg, fill, 0.33));
        for (var t = 0.31; Ratio(fg, selection) < 4.5 && t > 0.05; t -= 0.02)
            selection = Rounded(Mix(bg, fill, t));
        return new()
        {
            ["accent"] = fill.Hex(),
            ["accent_fg"] = onFill.Hex(),
            ["accent_text"] = text.Hex(),
            ["accent_deep"] = deep.Hex(),
            ["selection"] = selection.Hex(),
        };
    }

    /// <summary>The combinations plugins use: text colors on the surfaces they sit on, and the accent's own.</summary>
    static readonly Pair[] Pairs =
    [
        new("fg", "bg", 4.5), new("fg", "surface", 4.5), new("fg", "surface_raised", 4.5),
        new("fg", "selection", 4.5),
        new("fg_muted", "bg", 4.5), new("fg_muted", "surface_raised", 4.5),
        new("fg_subtle", "bg", 3),
        new("accent_text", "bg", 4.5), new("accent_text", "surface_raised", 4.5),
        new("accent_text", "accent_deep", 4.5),
        new("accent_fg", "accent", 4.5),
        new("accent", "bg", 3), new("border", "bg", 1.3),
        new("success", "bg", 4.5), new("warning", "bg", 4.5),
        new("danger", "bg", 4.5), new("info", "bg", 4.5),
    ];

    /// <summary>Contrast checks every pair plugins rely on; the ones below their minimum are the theme's problems.</summary>
    public static List<Pair> Contrast(Theme t)
    {
        var out_ = new List<Pair>();
        foreach (var p in Pairs)
        {
            try
            {
                // Exact: 2.9997 is not 3.
                out_.Add(p with { Ratio = Contrast(t.Colors.GetValueOrDefault(p.Fg, ""), t.Colors.GetValueOrDefault(p.Bg, "")) });
            }
            catch (FormatException) { }
        }
        return out_;
    }
}

public sealed partial class Theme
{
    /// <summary>
    /// WithAccent replaces the theme's accent tokens with ones derived from
    /// accent (see AccentTokens). The theme's own accent keeps the theme's
    /// hand-picked tokens.
    /// </summary>
    public void WithAccent(string accent)
    {
        if (string.Equals(accent, Colors.GetValueOrDefault("accent"), StringComparison.OrdinalIgnoreCase)) return;
        foreach (var (k, v) in ColorMath.AccentTokens(accent, Colors, Meta.Mode)) Colors[k] = v;
    }

    public List<Pair> Contrast() => ColorMath.Contrast(this);
}
