using MyArch.Config;
using MyArch.Themes;
using MyArch.Util;

namespace MyArch.Cli;

public static partial class Program
{
    static int CmdThemes(string[] args)
    {
        var fs = new Flags("themes").Bool("json", "everything about every theme, for the theme picker").Parse(args);
        var asJson = fs.IsSet("json");
        Settings cfg;
        try
        {
            cfg = Settings.Load();
        }
        catch (MyArchException)
        {
            cfg = new Settings();
        }
        var all = new List<Fields>(); // [] in JSON, never null
        foreach (var id in Theme.List(ThemeDirs()))
        {
            Theme t;
            try
            {
                t = Theme.Load(ThemeDirs(), id);
            }
            catch (MyArchException e)
            {
                if (!asJson) Console.WriteLine($"  {id,-14} {e.Message}");
                continue;
            }
            var (info, problems, accents) = Describe(t, cfg);
            all.Add(info);
            if (asJson) continue;
            var mark = " ";
            var shown = problems;
            if (id == cfg.Theme)
            {
                mark = "*";
                // As you have it: with your accent.
                foreach (var (color, accentProblems) in accents)
                    if (cfg.Accent != "" && color == cfg.Accent.ToLowerInvariant()) shown = accentProblems;
            }
            var text = string.Concat(shown.Select(p =>
                $"  {p.Fg} on {p.Bg} {GoFormat.Fixed(p.Ratio, 2)} < {GoFormat.Fixed(p.Min, 1)}"));
            if (text == "") text = "  contrast ok";
            Console.WriteLine($"{mark} {id,-14} {t.Meta.Mode,-5} {t.Meta.Name}{text}");
        }
        if (asJson)
            Console.WriteLine(GoJson.Marshal(new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["current"] = cfg.Theme,
                ["accent"] = cfg.Accent,
                ["themes"] = all,
            }));
        return 0;
    }

    /// <summary>
    /// A theme as the picker needs it: its tokens, the accents it suggests
    /// (each with its derived tokens) and its contrast problems.
    /// </summary>
    static (Fields Info, List<Pair> Problems, List<(string Color, List<Pair> Problems)> Accents) Describe(Theme t, Settings cfg)
    {
        var problems = Problems(t);
        var accents = new List<Fields>();
        var accentProblems = new List<(string, List<Pair>)>();
        // The theme's own accent first, then its suggestions, then the one in
        // config.toml if it's none of those.
        var seen = new HashSet<string>();
        foreach (var raw in new[] { t.Colors.GetValueOrDefault("accent", "") }.Concat(t.Meta.Accents).Append(cfg.Accent))
        {
            var a = raw.ToLowerInvariant();
            if (a == "" || !seen.Add(a)) continue;
            Theme with;
            try
            {
                with = Theme.Load(ThemeDirs(), t.Id);
                with.WithAccent(a);
            }
            catch (Exception e) when (e is MyArchException or FormatException)
            {
                continue;
            }
            var tokens = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var k in new[] { "accent", "accent_fg", "accent_text", "accent_deep", "selection" })
                tokens[k] = with.Colors.GetValueOrDefault(k);
            var p = Problems(with);
            accentProblems.Add((a, p));
            accents.Add(new Fields
            {
                { "color", a },     // as chosen (what config.toml keeps)
                { "tokens", tokens }, // what it becomes in this theme
                { "problems", Json(p) }, // the theme's, with this accent
            });
        }
        var info = new Fields
        {
            { "id", t.Id },
            { "name", t.Meta.Name },
            { "mode", t.Meta.Mode },
            { "description", t.Meta.Description },
            { "colors", new SortedDictionary<string, string>(t.Colors, StringComparer.Ordinal) },
            { "ansi", new SortedDictionary<string, string>(t.Ansi, StringComparer.Ordinal) },
            { "font", t.Font.Mono },
            { "radius", t.Shape.Radius },
            { "border", t.Shape.Border },
            { "terminal_opacity", t.Effects.TerminalOpacity },
            { "blur", t.Effects.Blur },
            { "wallpaper", t.Effects.Wallpaper },
            { "accents", accents },
            { "problems", Json(problems) },
        };
        return (info, problems, accentProblems);
    }

    /// <summary>
    /// Problems are the contrast pairs a theme fails, rounded down for showing
    /// (2.9997 is not 3.00).
    /// </summary>
    static List<Pair> Problems(Theme t) =>
        t.Contrast().Where(p => !p.OK).Select(p => p with { Ratio = Math.Floor(p.Ratio * 100) / 100 }).ToList();

    static List<Fields> Json(List<Pair> problems) =>
        problems.Select(p => new Fields { { "fg", p.Fg }, { "bg", p.Bg }, { "ratio", p.Ratio }, { "min", p.Min } }).ToList();
}
