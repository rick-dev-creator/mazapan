using System.Diagnostics;
using System.Text;
using Mazapan.Util;

namespace Mazapan.Themes;

/// <summary>A color of a picture, and how much of it there is (0..1).</summary>
public readonly record struct Swatch(string Hex, double Share);

/// <summary>
/// FromImage makes a theme out of a picture: its colors, found (k-means in
/// OKLab, where "close" is what looks close), become every token a theme
/// has, with each contrast the themes promise held. The picture is the
/// wallpaper; font, shape, motion and effects are kept from a theme given
/// (the one in use), so only the colors change.
/// </summary>
internal static class FromImage
{
    // --- reading a picture ---------------------------------------------------------

    /// <summary>
    /// A picture's pixels, small (64×64 is plenty for its colors, and the
    /// whole picture is in them, squeezed: its sides count as much as its
    /// middle). Decoded by ImageMagick or else ffmpeg (the next one when one
    /// can't), as an 8-bit PPM; what's transparent counts as mid grey, the
    /// same with either.
    /// </summary>
    public static List<Rgb> Pixels(string path)
    {
        if (!File.Exists(path)) throw new MazapanException($"{path}: no such file");
        var tries = new List<(string, string[])>
        {
            ("magick", [path + "[0]", "-auto-orient", "-background", "#808080", "-alpha", "remove", "-alpha", "off",
                "-resize", "64x64!", "-depth", "8", "ppm:-"]),
            ("ffmpeg", ["-nostdin", "-v", "error", "-i", path, "-frames:v", "1",
                "-filter_complex", "color=c=0x808080:s=64x64[g];[0:v]scale=64:64,format=rgba[p];[g][p]overlay=shortest=1,format=rgb24",
                "-f", "image2pipe", "-vcodec", "ppm", "-"]),
        };
        var why = new List<string>();
        foreach (var (tool, args) in tries)
        {
            try
            {
                return Ppm(Run(tool, args));
            }
            catch (System.ComponentModel.Win32Exception)
            {
                why.Add($"{tool}: not installed");
            }
            catch (MazapanException e)
            {
                why.Add(e.Message);
            }
        }
        throw new MazapanException("couldn't read the picture (it needs ImageMagick or ffmpeg): " + string.Join("; ", why));
    }

    /// <summary>
    /// The picture as a PNG at dest, for a wallpaper Qt can't show as it is
    /// (HEIC, AVIF, …).
    /// </summary>
    public static void ToPng(string path, string dest)
    {
        var why = new List<string>();
        foreach (var (tool, args) in new List<(string, string[])>
        {
            ("magick", [path + "[0]", "-auto-orient", dest]),
            ("ffmpeg", ["-nostdin", "-v", "error", "-y", "-i", path, "-frames:v", "1", dest]),
        })
        {
            try
            {
                Run(tool, args, allowEmpty: true);
                if (File.Exists(dest)) return;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                why.Add($"{tool}: not installed");
            }
            catch (MazapanException e)
            {
                why.Add(e.Message);
            }
        }
        throw new MazapanException("couldn't make a PNG of the picture: " + string.Join("; ", why));
    }

    /// <summary>What Qt shows as a wallpaper as it is.</summary>
    public static bool Shown(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif";

    static byte[] Run(string tool, string[] args, bool allowEmpty = false)
    {
        var psi = new ProcessStartInfo(tool)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var err = p.StandardError.ReadToEndAsync();
        var outTask = Task.Run(() =>
        {
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            return ms.ToArray();
        });
        // A picture that takes this long is one it can't read.
        if (!p.WaitForExit(30000))
        {
            try
            {
                p.Kill(true);
            }
            catch (InvalidOperationException) { }
            throw new MazapanException($"{tool} took too long");
        }
        var bytes = outTask.Result;
        if (p.ExitCode != 0 || (bytes.Length == 0 && !allowEmpty))
            throw new MazapanException($"{tool}: {err.Result.Trim().Split('\n').LastOrDefault()}");
        return bytes;
    }

    /// <summary>A binary PPM (P6, 8 bits) into its pixels.</summary>
    public static List<Rgb> Ppm(byte[] b)
    {
        var i = 0;
        string Token()
        {
            while (i < b.Length)
            {
                if (b[i] == '#') while (i < b.Length && b[i] != '\n') i++;
                else if (char.IsWhiteSpace((char)b[i])) i++;
                else break;
            }
            var start = i;
            while (i < b.Length && !char.IsWhiteSpace((char)b[i])) i++;
            return Encoding.ASCII.GetString(b, start, i - start);
        }
        if (Token() != "P6") throw new MazapanException("not a binary PPM");
        if (!int.TryParse(Token(), out var w) || !int.TryParse(Token(), out var h) || !int.TryParse(Token(), out var max) || max != 255 || w <= 0 || h <= 0)
            throw new MazapanException("a PPM of 8-bit pixels was expected");
        i++; // the one whitespace before the pixels
        if (b.Length - i < w * h * 3) throw new MazapanException("the PPM is cut short");
        var out_ = new List<Rgb>(w * h);
        for (var n = 0; n < w * h; n++, i += 3)
            out_.Add(new Rgb(b[i] / 255.0, b[i + 1] / 255.0, b[i + 2] / 255.0));
        return out_;
    }

    // --- its colors ------------------------------------------------------------------

    /// <summary>
    /// The picture's main colors, most of it first: k-means in OKLab,
    /// started from colors far apart (so it's the same every time, and a
    /// small bright patch isn't swallowed by the rest).
    /// </summary>
    public static List<Swatch> Palette(IReadOnlyList<Rgb> pixels, int k = 8)
    {
        if (pixels.Count == 0) throw new MazapanException("the picture has no pixels");
        var pts = pixels.Select(p => p.OkLab()).ToArray();
        k = Math.Min(k, pts.Length);
        static double D(Lab a, Lab b) => (a.L - b.L) * (a.L - b.L) + (a.A - b.A) * (a.A - b.A) + (a.B - b.B) * (a.B - b.B);
        // First: the one nearest the mean; then each time the farthest from
        // those taken.
        var mean = new Lab(pts.Average(p => p.L), pts.Average(p => p.A), pts.Average(p => p.B));
        var centers = new List<Lab> { pts.MinBy(p => D(p, mean)) };
        while (centers.Count < k)
        {
            var far = pts.MaxBy(p => centers.Min(c => D(p, c)));
            if (centers.Min(c => D(far, c)) < 1e-6) break; // fewer colors than k
            centers.Add(far);
        }
        var assign = new int[pts.Length];
        for (var round = 0; round < 20; round++)
        {
            var moved = false;
            for (var n = 0; n < pts.Length; n++)
            {
                var best = 0;
                for (var c = 1; c < centers.Count; c++)
                    if (D(pts[n], centers[c]) < D(pts[n], centers[best])) best = c;
                if (assign[n] != best) moved = true;
                assign[n] = best;
            }
            for (var c = 0; c < centers.Count; c++)
            {
                var members = Enumerable.Range(0, pts.Length).Where(n => assign[n] == c).Select(n => pts[n]).ToList();
                if (members.Count > 0)
                    centers[c] = new Lab(members.Average(p => p.L), members.Average(p => p.A), members.Average(p => p.B));
            }
            if (!moved && round > 0) break;
        }
        return Enumerable.Range(0, centers.Count)
            .Select(c => new Swatch(centers[c].ToRgb().Item1.Hex(), assign.Count(a => a == c) / (double)pts.Length))
            .Where(s => s.Share > 0)
            .OrderByDescending(s => s.Share)
            .ThenBy(s => s.Hex, StringComparer.Ordinal)
            .ToList();
    }

    // --- a theme from them -------------------------------------------------------------

    static double Chroma(Lab l) => Math.Sqrt(l.A * l.A + l.B * l.B);
    static double Hue(Lab l) => Math.Atan2(l.B, l.A);

    /// <summary>A color at lightness l, chroma c, hue h (OKLCH), made to exist on screen.</summary>
    static Rgb Lch(double l, double c, double h)
    {
        for (var k = 1.0; k >= 0; k -= 0.02)
        {
            var (rgb, ok) = new Lab(l, c * k * Math.Cos(h), c * k * Math.Sin(h)).ToRgb();
            if (ok) return rgb;
        }
        return new Lab(l, 0, 0).ToRgb().Item1;
    }

    /// <summary>"dark" when the picture is mostly dark, else "light".</summary>
    public static string ModeOf(IReadOnlyList<Swatch> palette) =>
        palette.Sum(s => Rgb.Parse(s.Hex).OkLab().L * s.Share) < 0.6 ? "dark" : "light";

    /// <summary>
    /// The accent: the color that stands out, as a person would pick it. A
    /// color's family (its hue, with its lighter and darker shades: a
    /// jellyfish's three oranges) counts as one; vivid and plentiful score
    /// higher, and a hue unlike the picture's main one (the subject, not the
    /// backdrop) much higher. Then others far from it in hue, as the
    /// theme's suggestions.
    /// </summary>
    public static List<string> Accents(IReadOnlyList<Swatch> palette)
    {
        var labs = palette.Select(s => (s, lab: Rgb.Parse(s.Hex).OkLab())).ToList();
        var mainHue = Hue(labs[0].lab);
        var mainChroma = Chroma(labs[0].lab);
        static double Apart(double a, double b) => Math.Abs(Math.IEEERemainder(a - b, 2 * Math.PI));
        var colorful = labs.Where(x => Chroma(x.lab) >= 0.04).ToList();
        var scored = colorful.Select(x =>
        {
            var family = colorful.Where(y => Apart(Hue(y.lab), Hue(x.lab)) < 0.5).Sum(y => y.s.Share);
            // Unlike the backdrop: all of it when the backdrop has a hue of
            // its own, less when it's grey (any hue stands out on grey).
            var unlike = mainChroma < 0.04 ? 1 : 0.4 + 0.6 * Apart(Hue(x.lab), mainHue) / Math.PI;
            return (x.lab, score: Chroma(x.lab) * Math.Sqrt(family) * unlike);
        }).OrderByDescending(x => x.score).ToList();
        var picked = new List<Lab>();
        foreach (var (lab, _) in scored)
        {
            if (picked.Any(p => Apart(Hue(p), Hue(lab)) < 0.5)) continue;
            picked.Add(lab);
            if (picked.Count == 4) break;
        }
        // A picture with no color at all: a calm blue.
        if (picked.Count == 0) picked.Add(Rgb.Parse("#5b8def").OkLab());
        return picked.Select(l => Lch(Math.Max(0.55, Math.Min(0.78, l.L)), Math.Max(0.1, Chroma(l)), Hue(l)).Hex()).ToList();
    }

    /// <summary>
    /// A whole theme's colors from a palette, for a mode: surfaces in the
    /// picture's main hue (barely tinted), text neutral but for that tint,
    /// the accent from the picture, the status colors and the terminal's in
    /// their usual hues; every contrast promised, held.
    /// </summary>
    public static (Dictionary<string, string> Colors, Dictionary<string, string> Ansi, List<string> Accents) Build(IReadOnlyList<Swatch> palette, string mode)
    {
        var dark = mode == "dark";
        var accents = Accents(palette);
        // The surfaces' hue: the picture's main color, as much as it has one.
        var main = palette.Select(s => Rgb.Parse(s.Hex).OkLab()).First();
        var hue = Hue(main);
        var tint = Math.Min(Chroma(main), dark ? 0.03 : 0.018);
        Rgb Surface(double l) => Lch(l, tint, hue);
        Rgb Text(double l) => Lch(l, Math.Min(tint, 0.012), hue);

        var c = new Dictionary<string, string>
        {
            ["bg"] = Surface(dark ? 0.20 : 0.975).Hex(),
            ["bg_alt"] = Surface(dark ? 0.17 : 0.945).Hex(),
            ["surface"] = Surface(dark ? 0.235 : 0.95).Hex(),
            ["surface_raised"] = Surface(dark ? 0.27 : 0.925).Hex(),
            ["border"] = Surface(dark ? 0.36 : 0.83).Hex(),
            ["fg"] = Text(dark ? 0.93 : 0.24).Hex(),
            ["fg_muted"] = Text(dark ? 0.78 : 0.42).Hex(),
            ["fg_subtle"] = Text(dark ? 0.64 : 0.56).Hex(),
        };
        // Text: as close to its lightness as holds on every surface.
        Rgb[] Surfaces(params string[] names) => names.Select(n => Rgb.Parse(c[n])).ToArray();
        c["fg"] = ColorMath.Against(Rgb.Parse(c["fg"]), Surfaces("bg", "surface", "surface_raised"), 7, !dark).Hex();
        c["fg_muted"] = ColorMath.Against(Rgb.Parse(c["fg_muted"]), Surfaces("bg", "surface_raised"), 4.5, !dark).Hex();
        c["fg_subtle"] = ColorMath.Against(Rgb.Parse(c["fg_subtle"]), Surfaces("bg"), 3, !dark).Hex();
        c["border"] = ColorMath.Against(Rgb.Parse(c["border"]), Surfaces("bg"), 1.3, !dark).Hex();
        // Status: the usual hues, a little of the picture's warmth left out.
        double Deg(double d) => d * Math.PI / 180;
        string Status(double h) =>
            ColorMath.Against(Lch(dark ? 0.76 : 0.52, 0.14, Deg(h)), Surfaces("bg"), 4.5, !dark).Hex();
        c["success"] = Status(145);
        c["warning"] = Status(80);
        c["danger"] = Status(25);
        c["info"] = Status(245);
        // The accent and everything derived from it (text on it, its text,
        // its deep fill, the selection).
        foreach (var (k, v) in ColorMath.AccentTokens(accents[0], c, mode)) c[k] = v;

        // The terminal's colors: the usual hues, light enough to read.
        var a = new Dictionary<string, string>();
        var hues = new (string Name, double H)[] { ("red", 25), ("green", 145), ("yellow", 85), ("blue", 250), ("magenta", 330), ("cyan", 195) };
        foreach (var (name, h) in hues)
        {
            a[name] = ColorMath.Against(Lch(dark ? 0.70 : 0.50, 0.13, Deg(h)), Surfaces("bg"), 4.5, !dark).Hex();
            a["bright_" + name] = ColorMath.Against(Lch(dark ? 0.80 : 0.42, 0.12, Deg(h)), Surfaces("bg"), 4.5, !dark).Hex();
        }
        a["black"] = dark ? c["bg_alt"] : c["fg"];
        a["bright_black"] = c["fg_subtle"];
        a["white"] = dark ? c["fg_muted"] : c["surface_raised"];
        a["bright_white"] = dark ? c["fg"] : c["bg"];
        return (c, a, accents);
    }

    /// <summary>
    /// A theme.toml for a picture: its colors from Build, the rest from like
    /// (the theme in use), the picture as the wallpaper (next to it).
    /// </summary>
    public static string Toml(string name, string mode, string description, string wallpaper,
        Dictionary<string, string> colors, Dictionary<string, string> ansi, List<string> accents, Theme like)
    {
        var b = new StringBuilder();
        b.Append("# Made by mazapan from a picture (mazapan themes from-image): its colors,\n");
        b.Append("# every contrast checked. Edit it as any theme.\n\n");
        b.Append("[meta]\n");
        Config.TomlWriter.Key(b, "name", name);
        Config.TomlWriter.Key(b, "mode", mode);
        Config.TomlWriter.Key(b, "description", description);
        Config.TomlWriter.Key(b, "accents", accents.Skip(1).ToList());
        void Table(string title, Dictionary<string, string> m, string[] order)
        {
            b.Append('\n').Append('[').Append(title).Append("]\n");
            foreach (var k in order) Config.TomlWriter.Key(b, k, m[k]);
        }
        Table("colors", colors, Theme.RequiredColors);
        Table("ansi", ansi, Theme.RequiredAnsi);
        b.Append("\n[font]\n");
        Config.TomlWriter.Key(b, "mono", like.Font.Mono);
        Config.TomlWriter.Key(b, "ui", like.Font.UI);
        Config.TomlWriter.Key(b, "size", like.Font.Size);
        b.Append("\n[shape]\n");
        Config.TomlWriter.Key(b, "radius", like.Shape.Radius);
        Config.TomlWriter.Key(b, "border", like.Shape.Border);
        Config.TomlWriter.Key(b, "gap_in", like.Shape.GapIn);
        Config.TomlWriter.Key(b, "gap_out", like.Shape.GapOut);
        b.Append("\n[effects]\n");
        Config.TomlWriter.Key(b, "terminal_opacity", like.Effects.TerminalOpacity);
        Config.TomlWriter.Key(b, "blur", like.Effects.Blur);
        Config.TomlWriter.Key(b, "wallpaper", wallpaper);
        b.Append("\n[motion]\n");
        Config.TomlWriter.Key(b, "enabled", like.Motion.Enabled);
        Config.TomlWriter.Key(b, "duration_ms", like.Motion.DurationMS);
        Config.TomlWriter.Key(b, "curve", like.Motion.Curve);
        Config.TomlWriter.Key(b, "response_ms", like.Motion.ResponseMS);
        Config.TomlWriter.Key(b, "damping", like.Motion.Damping);
        return b.ToString();
    }
}
