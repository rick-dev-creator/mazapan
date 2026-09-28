using System.Text.RegularExpressions;
using MyArch.Util;

namespace MyArch.Themes;

/// <summary>
/// A theme is a set of semantic tokens. Plugins read tokens by name, so
/// every name in RequiredColors and RequiredAnsi is part of the plugin API:
/// removing or renaming one is a breaking change.
/// </summary>
public sealed partial class Theme
{
    public static readonly string[] RequiredColors =
    [
        "bg", "bg_alt", "surface", "surface_raised", "border",
        "fg", "fg_muted", "fg_subtle",
        "accent", "accent_fg", "accent_text", "accent_deep", "selection",
        "success", "warning", "danger", "info",
    ];

    public static readonly string[] RequiredAnsi =
    [
        "black", "red", "green", "yellow", "blue", "magenta", "cyan", "white",
        "bright_black", "bright_red", "bright_green", "bright_yellow",
        "bright_blue", "bright_magenta", "bright_cyan", "bright_white",
    ];

    public string Id { get; init; } = "";
    public string Dir { get; init; } = "";

    public sealed class MetaTable
    {
        public string Name = "";
        public string Mode = "";
        public string Description = "";
        /// <summary>Accents the theme suggests besides its own (the picker offers them): #rrggbb.</summary>
        public List<string> Accents = [];
    }

    public sealed class FontTable
    {
        public string Mono = "";
        public string UI = "";
        public double Size;
    }

    public sealed class ShapeTable
    {
        public long Radius, Border, GapIn, GapOut;
    }

    /// <summary>Effects are optional: without them, windows are opaque and there's no blur.</summary>
    public sealed class EffectsTable
    {
        /// <summary>Terminal background opacity, 0.5 to 1.</summary>
        public double TerminalOpacity;
        /// <summary>Blur what's behind see-through windows.</summary>
        public bool Blur;
        /// <summary>
        /// The wallpaper drawn from the theme: "grid" (a faint grid in the
        /// accent over a gradient), "plain" (bg_alt), or a path to an image.
        /// </summary>
        public string Wallpaper = "";
    }

    public sealed class MotionTable
    {
        public bool Enabled;
        /// <summary>Short transitions (fades, border color): a cubic-bezier.</summary>
        public long DurationMS;
        public List<double> Curve = [0, 0, 0, 0];
        /// <summary>
        /// Movement (windows, workspaces): a spring, described the way tablet
        /// UIs do (SwiftUI's spring(response:dampingFraction:)).
        /// </summary>
        public long ResponseMS; // period of the spring
        public double Damping;  // 1 = no overshoot, lower = bouncier
    }

    public MetaTable Meta { get; } = new();
    public Dictionary<string, string> Colors { get; internal set; } = [];
    public Dictionary<string, string> Ansi { get; internal set; } = [];
    public FontTable Font { get; } = new();
    public ShapeTable Shape { get; } = new();
    public EffectsTable Effects { get; } = new();
    public MotionTable Motion { get; } = new();

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?\z")]
    private static partial Regex HexColor();

    /// <summary>Load finds theme id in the first directory of dirs that has it.</summary>
    public static Theme Load(IEnumerable<string> dirs, string id)
    {
        var list = dirs.ToList();
        foreach (var d in list)
        {
            var dir = Paths.Join(d, id);
            var path = Paths.Join(dir, "theme.toml");
            var table = Toml.ReadFile(path);
            if (table == null) continue;
            var t = new Theme { Id = id, Dir = dir };
            var r = new TomlReader(table, path);
            var meta = r.Sub("meta");
            t.Meta.Name = meta.String("name");
            t.Meta.Mode = meta.String("mode");
            t.Meta.Description = meta.String("description");
            t.Meta.Accents = meta.Strings("accents");
            t.Colors = r.StringMap("colors");
            t.Ansi = r.StringMap("ansi");
            var font = r.Sub("font");
            t.Font.Mono = font.String("mono");
            t.Font.UI = font.String("ui");
            t.Font.Size = font.Float("size");
            var shape = r.Sub("shape");
            t.Shape.Radius = shape.Int("radius");
            t.Shape.Border = shape.Int("border");
            t.Shape.GapIn = shape.Int("gap_in");
            t.Shape.GapOut = shape.Int("gap_out");
            var fx = r.Sub("effects");
            t.Effects.TerminalOpacity = fx.Float("terminal_opacity");
            t.Effects.Blur = fx.Bool("blur");
            t.Effects.Wallpaper = fx.String("wallpaper");
            var motion = r.Sub("motion");
            t.Motion.Enabled = motion.Bool("enabled");
            t.Motion.DurationMS = motion.Int("duration_ms");
            if (motion.Has("curve"))
            {
                var curve = motion.Floats("curve");
                if (curve.Count != 4) throw new MyArchException($"{path}: motion.curve: want 4 numbers");
                t.Motion.Curve = curve;
            }
            t.Motion.ResponseMS = motion.Int("response_ms");
            t.Motion.Damping = motion.Float("damping");
            r.Done();
            try
            {
                t.Validate();
            }
            catch (MyArchException e)
            {
                throw new MyArchException($"{path}: {e.Message}");
            }
            return t;
        }
        throw new MyArchException($"theme \"{id}\" not found in {string.Join(", ", list)}");
    }

    /// <summary>List returns the ids of every theme found in dirs, first match wins.</summary>
    public static List<string> List(IEnumerable<string> dirs)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            foreach (var e in Directory.GetDirectories(d))
                if (File.Exists(Path.Join(e, "theme.toml"))) ids.Add(Path.GetFileName(e));
        }
        return [.. ids];
    }

    public void Validate()
    {
        var problems = new List<string>();
        void Check(string table, Dictionary<string, string> m, string[] required)
        {
            foreach (var k in required)
            {
                if (!m.TryGetValue(k, out var v)) problems.Add($"[{table}] missing \"{k}\"");
                else if (!HexColor().IsMatch(v)) problems.Add($"[{table}] {k} = \"{v}\" is not #rrggbb or #rrggbbaa");
            }
        }
        foreach (var a in Meta.Accents)
            if (!HexColor().IsMatch(a)) problems.Add($"[meta] accents: \"{a}\" is not #rrggbb");
        Check("colors", Colors, RequiredColors);
        Check("ansi", Ansi, RequiredAnsi);
        if (Meta.Mode is not ("dark" or "light"))
            problems.Add($"[meta] mode = \"{Meta.Mode}\" must be \"dark\" or \"light\"");
        if (Motion.ResponseMS <= 0 || Motion.Damping <= 0 || Motion.Damping > 2)
            problems.Add("[motion] needs response_ms > 0 and 0 < damping <= 2");
        if (Effects.TerminalOpacity == 0) Effects.TerminalOpacity = 1;
        if (Effects.TerminalOpacity < 0.5 || Effects.TerminalOpacity > 1)
            problems.Add("[effects] terminal_opacity must be between 0.5 and 1");
        switch (Effects.Wallpaper)
        {
            case "":
                Effects.Wallpaper = "plain";
                break;
            case "plain" or "grid":
                break;
            default:
                // An image: next to theme.toml unless the path is absolute.
                if (!Effects.Wallpaper.StartsWith('/')) Effects.Wallpaper = Paths.Join(Dir, Effects.Wallpaper);
                if (!Paths.Exists(Effects.Wallpaper))
                    problems.Add($"[effects] wallpaper: stat {Effects.Wallpaper}: no such file or directory");
                break;
        }
        if (Font.Mono == "" || Font.UI == "" || Font.Size <= 0)
            problems.Add("[font] needs mono, ui and a positive size");
        if (problems.Count > 0)
            throw new MyArchException("invalid theme:\n  " + string.Join("\n  ", problems));
    }

    /// <summary>Color returns a token from [colors], falling back to [ansi].</summary>
    public string Color(string name)
    {
        if (Colors.TryGetValue(name, out var v)) return v;
        if (Ansi.TryGetValue(name, out v)) return v;
        throw new MyArchException($"theme \"{Id}\" has no color \"{name}\"");
    }
}
