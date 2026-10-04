using Mazapan.Config;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    static int CmdThemes(string[] args)
    {
        if (args.Length > 0 && args[0] == "from-image") return CmdThemesFromImage(args[1..]);
        if (args.Length > 0 && args[0] == "import-omarchy") return CmdThemesImportOmarchy(args[1..]);
        if (args.Length > 0 && args[0] == "remove") return CmdThemesRemove(args[1..]);
        var fs = new Flags("themes").Bool("json", "everything about every theme, for the theme picker").Parse(args);
        var asJson = fs.IsSet("json");
        Settings cfg;
        try
        {
            cfg = Settings.Load();
        }
        catch (MazapanException)
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
            catch (MazapanException e)
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
            catch (Exception e) when (e is MazapanException or FormatException)
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

    /// <summary>Where themes of your own live (the first of ThemeDirs).</summary>
    static string UserThemes() => ThemeDirs()[0];

    /// <summary>
    /// themes from-image PICTURE: a theme made of the picture's colors (and
    /// the picture as its wallpaper), in the user's themes; --apply switches
    /// to it. Made again from the same picture, it's the same theme, made
    /// over.
    /// </summary>
    static int CmdThemesFromImage(string[] args)
    {
        // Flags before or after the picture (-apply or --apply).
        var flags = new List<string>();
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--")
            {
                rest.AddRange(args[(i + 1)..]);
                break;
            }
            if (args[i].Length < 2 || args[i][0] != '-') rest.Add(args[i]);
            else
            {
                flags.Add(args[i]);
                if (args[i].TrimStart('-') is "name" or "mode" && i + 1 < args.Length) flags.Add(args[++i]);
            }
        }
        var fs = new Flags("themes from-image")
            .String("name", "the theme's name (default: the picture's)")
            .String("mode", "dark or light (default: as the picture is)")
            .Bool("apply", "switch to it")
            .Bool("json", "what was made: id, name, mode, colors, accents, palette, problems")
            .Parse([.. flags, "--", .. rest]);
        if (fs.Rest.Count != 1) throw new MazapanException("usage: mazapan themes from-image PICTURE [--name NAME] [--mode dark|light] [--apply] [--json]");
        var path = Path.GetFullPath(Paths.ExpandHome(fs.Rest[0]));
        var mode = fs.Get("mode");
        if (mode is not ("" or "dark" or "light")) throw new MazapanException($"--mode {mode}: dark or light");

        var palette = FromImage.Palette(FromImage.Pixels(path));
        if (mode == "") mode = FromImage.ModeOf(palette);
        var (colors, ansi, accents) = FromImage.Build(palette, mode);

        // Font, shape, motion, effects: the theme in use, or else the first
        // that loads.
        Settings cfg;
        try
        {
            cfg = Settings.Load();
        }
        catch (MazapanException)
        {
            cfg = new Settings();
        }
        Theme? like = null;
        foreach (var candidate in new[] { cfg.Theme }.Concat(Theme.List(ThemeDirs())).Where(x => x != ""))
        {
            try
            {
                like = Theme.Load(ThemeDirs(), candidate);
                break;
            }
            catch (MazapanException) { }
        }
        if (like == null) throw new MazapanException("no theme loads to take the font and shape from");

        var stem = Path.GetFileNameWithoutExtension(path);
        var id = ThemeIdFor(stem, path);
        var dir = Paths.Join(UserThemes(), id);
        // Kept as it is when Qt shows it as a wallpaper; else a PNG of it.
        var picture = FromImage.Shown(path) ? "wallpaper" + Path.GetExtension(path).ToLowerInvariant() : "wallpaper.png";
        var name = fs.Get("name") != "" ? fs.Get("name") : Readable(stem);
        var text = FromImage.Toml(name, mode, $"Colors from {Path.GetFileName(path)}", picture, colors, ansi, accents, like);

        PutTheme(id, text, path, picture);
        var made = Theme.Load(ThemeDirs(), id);
        var problems = Problems(made);

        if (fs.IsSet("json"))
        {
            // What apply says doesn't mix with the JSON.
            var outWas = Console.Out;
            var code = 0;
            if (fs.IsSet("apply"))
            {
                Console.SetOut(TextWriter.Null);
                try
                {
                    code = CmdApply(["--theme", id, "--accent", "theme"]);
                }
                finally
                {
                    Console.SetOut(outWas);
                }
            }
            Console.WriteLine(GoJson.Marshal(new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["name"] = name,
                ["mode"] = mode,
                ["colors"] = new SortedDictionary<string, string>(made.Colors, StringComparer.Ordinal),
                ["accents"] = accents,
                ["palette"] = palette.Select(p => new Fields { { "hex", p.Hex }, { "share", Math.Round(p.Share, 3) } }).ToList(),
                ["problems"] = Json(problems),
                ["applied"] = fs.IsSet("apply"),
            }));
            return code;
        }
        static string Block(string hex)
        {
            var c = Rgb.Parse(hex);
            return $"\u001b[48;2;{(int)(c.R * 255)};{(int)(c.G * 255)};{(int)(c.B * 255)}m  \u001b[0m";
        }
        Console.WriteLine($"made theme {id} ({name}, {mode}) from {Tilde(path)}");
        Console.WriteLine("  picture  " + string.Concat(palette.Select(p => Block(p.Hex))));
        Console.WriteLine("  theme    " + string.Concat(new[] { "bg", "surface_raised", "fg", "accent", "success", "warning", "danger", "info" }.Select(k => Block(made.Colors[k]))));
        Console.WriteLine(problems.Count == 0 ? "  contrast ok" : "  contrast: " + string.Join(", ", problems.Select(p => $"{p.Fg} on {p.Bg} {p.Ratio}")));
        // The picture's own accent, not one chosen for another theme.
        if (fs.IsSet("apply")) return CmdApply(["--theme", id, "--accent", "theme"]);
        Console.WriteLine($"  use it: mazapan apply --theme {id}");
        return 0;
    }

    /// <summary>
    /// A theme of the person's own, put in place: written aside (a folder of
    /// its own: two at once don't meet), checked, then swapped in; the old one
    /// goes only once the new one is in place, so a theme that loaded is never
    /// lost halfway. Its picture (if any) beside it, as it is when Qt shows
    /// it, else as a PNG.
    /// </summary>
    static void PutTheme(string id, string text, string? picturePath, string picture)
    {
        var dir = Paths.Join(UserThemes(), id);
        var staging = Paths.Join(UserThemes(), ".staging");
        var tag = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
        var tmp = Paths.Join(staging, tag, id);
        var old = Paths.Join(staging, tag + ".old");
        try
        {
            Files.CreateDirectory(tmp);
            if (picturePath != null)
            {
                if (FromImage.Shown(picturePath)) File.Copy(picturePath, Paths.Join(tmp, picture));
                else FromImage.ToPng(picturePath, Paths.Join(tmp, picture));
            }
            File.WriteAllText(Paths.Join(tmp, "theme.toml"), text);
            Theme.Load([Paths.Dir(tmp)], id);
            if (Directory.Exists(dir)) Directory.Move(dir, old);
            try
            {
                Directory.Move(tmp, dir);
            }
            catch (IOException)
            {
                // The new one couldn't go in: the old one back, never lost.
                if (Directory.Exists(old) && !Directory.Exists(dir)) Directory.Move(old, dir);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(Paths.Dir(tmp))) Directory.Delete(Paths.Dir(tmp), true);
            if (Directory.Exists(old)) Directory.Delete(old, true);
        }
    }

    /// <summary>The theme in use (for the font, shape, motion and effects a new one takes), or else the first that loads.</summary>
    static Theme LikeTheme()
    {
        Settings cfg;
        try { cfg = Settings.Load(); }
        catch (MazapanException) { cfg = new Settings(); }
        foreach (var candidate in new[] { cfg.Theme }.Concat(Theme.List(ThemeDirs())).Where(x => x != ""))
        {
            try { return Theme.Load(ThemeDirs(), candidate); }
            catch (MazapanException) { }
        }
        throw new MazapanException("no theme loads to take the font and shape from");
    }

    const string OmarchyRepo = "https://github.com/basecamp/omarchy";

    /// <summary>
    /// themes import-omarchy [DIR|URL]…: Omarchy's themes made Mazapán's
    /// (omarchy-NAME), from a folder of them, one theme's folder, or a git
    /// repository over https (a theme's, or Omarchy's own, whose themes/
    /// alone is fetched). With nothing named: the Omarchy themes on this
    /// computer, else Omarchy's repository. Imported again, a theme is made
    /// over.
    /// </summary>
    static int CmdThemesImportOmarchy(string[] args)
    {
        var fs = new Flags("themes import-omarchy")
            .Bool("apply", "switch to it (when one is imported)")
            .Bool("force", "import over one edited since it was imported")
            .Bool("json", "what was imported: id, name, mode, problems")
            .Parse(args);
        var sources = fs.Rest.ToList();
        var temps = new Dictionary<string, string>(); // a clone's folder -> its address
        try
        {
            var dirs = new List<string>();
            if (sources.Count == 0)
            {
                foreach (var d in new[] { "/usr/share/omarchy/themes", Paths.ExpandHome("~/.config/omarchy/themes") })
                    if (Directory.Exists(d)) dirs.Add(d);
                if (dirs.Count == 0) sources.Add(OmarchyRepo);
            }
            foreach (var s in sources)
            {
                if (s.Contains("://", StringComparison.Ordinal) || s.StartsWith("git@", StringComparison.Ordinal))
                {
                    var tmp = Clone(s);
                    temps[tmp] = s;
                    dirs.Add(tmp);
                }
                else
                {
                    var d = Path.GetFullPath(Paths.ExpandHome(s));
                    if (!Directory.Exists(d)) throw new MazapanException($"{s}: no such folder");
                    dirs.Add(d);
                }
            }
            // A theme's folder, or a folder of them (a repository's themes/ too).
            var found = new List<(string Dir, string Name)>();
            foreach (var d in dirs)
            {
                var name = temps.TryGetValue(d, out var url) ? RepoName(url) : Path.GetFileName(d.TrimEnd('/'));
                if (Mazapan.Themes.Omarchy.Palette(d) != null) { found.Add((d, name)); continue; }
                var under = Directory.Exists(Path.Join(d, "themes")) ? Path.Join(d, "themes") : d;
                foreach (var sub in Directory.GetDirectories(under).Order(StringComparer.Ordinal))
                    if (File.Exists(Path.Join(sub, "colors.toml")) || File.Exists(Path.Join(sub, "alacritty.toml")))
                        found.Add((sub, Path.GetFileName(sub)));
            }
            if (found.Count == 0) throw new MazapanException("no Omarchy theme there (a folder with colors.toml or alacritty.toml)");
            if (fs.IsSet("apply") && found.Count != 1) throw new MazapanException($"--apply: {found.Count} themes found; apply one with mazapan apply --theme ID");

            var like = LikeTheme();
            var made = new List<(string Id, string Name, string Mode, List<Pair> Problems)>();
            var failed = new List<string>();
            foreach (var (dir, raw) in found)
            {
                try
                {
                    var palette = Mazapan.Themes.Omarchy.Palette(dir)!;
                    var (colors, ansi, accents, mode) = Mazapan.Themes.Omarchy.Convert(palette);
                    var id = Mazapan.Themes.Omarchy.Id(raw);
                    var name = Mazapan.Themes.Omarchy.Readable(id["omarchy-".Length..]);
                    // One edited since it was imported (or put there by hand): the person's, kept.
                    var existing = Paths.Join(UserThemes(), id);
                    if (Directory.Exists(existing) && !fs.IsSet("force") && !AsImported(existing))
                    {
                        failed.Add($"{raw}: {id} was edited since it was imported: kept (--force to import it over)");
                        continue;
                    }
                    var bg = Mazapan.Themes.Omarchy.Wallpaper(dir);
                    var picture = bg == null ? "grid" : FromImage.Shown(bg) ? "wallpaper" + Path.GetExtension(bg).ToLowerInvariant() : "wallpaper.png";
                    var text = FromImage.Toml(name, mode, $"Omarchy's {name}", picture, colors, ansi, accents, like)
                        .Replace("# Made by mazapan from a picture (mazapan themes from-image): its colors,\n# every contrast checked. Edit it as any theme.\n",
                            "# Imported from Omarchy (mazapan themes import-omarchy): its palette, the\n# accent's tokens and the status colors made to read. Edit it as any theme.\n", StringComparison.Ordinal);
                    PutTheme(id, text, bg, picture);
                    // What was written, to tell an edit from it next time.
                    File.WriteAllText(Paths.Join(UserThemes(), id, ".imported"), Hex(text) + "\n");
                    made.Add((id, name, mode, Problems(Theme.Load(ThemeDirs(), id))));
                }
                catch (Exception e) when (e is MazapanException or FormatException or KeyNotFoundException or IOException)
                {
                    failed.Add($"{raw}: {e.Message}");
                }
            }
            if (fs.IsSet("json"))
            {
                Console.WriteLine(GoJson.Marshal(new Fields
                {
                    { "imported", made.Select(m => new Fields { { "id", m.Id }, { "name", m.Name }, { "mode", m.Mode }, { "problems", Json(m.Problems) } }).ToList() },
                    { "failed", failed },
                }));
            }
            else
            {
                foreach (var m in made)
                    Console.WriteLine($"  {m.Id,-28} {m.Name} ({m.Mode})" + (m.Problems.Count == 0 ? "" : $"  {Style.Dim}contrast: {string.Join(", ", m.Problems.Select(p => $"{p.Fg} on {p.Bg} {p.Ratio}"))}{Style.Reset}"));
                foreach (var f in failed) Console.WriteLine($"  {Style.Dim}not imported: {f}{Style.Reset}");
                Console.WriteLine($"{made.Count} theme{(made.Count == 1 ? "" : "s")} from Omarchy" + (made.Count > 0 && !fs.IsSet("apply") ? $": mazapan apply --theme {made[0].Id}" : ""));
            }
            if (fs.IsSet("apply") && made.Count == 1)
            {
                if (fs.IsSet("json")) Console.SetOut(TextWriter.Null);
                return CmdApply(["--theme", made[0].Id, "--accent", "theme"]);
            }
            return made.Count > 0 ? 0 : 1;
        }
        finally
        {
            foreach (var t in temps.Keys) try { Directory.Delete(t, true); } catch (IOException) { }
        }
    }

    static string Hex(string text) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));

    /// <summary>Its theme.toml still as the import wrote it.</summary>
    static bool AsImported(string dir)
    {
        try
        {
            return File.ReadAllText(Paths.Join(dir, ".imported")).Trim() == Hex(File.ReadAllText(Paths.Join(dir, "theme.toml")));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>"https://github.com/me/omarchy-dune-theme.git" → "omarchy-dune-theme".</summary>
    static string RepoName(string url) => Path.GetFileName(url.TrimEnd('/')).Replace(".git", "", StringComparison.Ordinal);

    /// <summary>A repository, shallow, in a folder of its own (Omarchy's: its themes/ alone). Over https only: never a command through git's other transports.</summary>
    static string Clone(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != "https" || u.Host == "")
            throw new MazapanException($"{url}: a repository over https");
        var tmp = Paths.Join(Path.GetTempPath(), $"mazapan-omarchy-{Environment.ProcessId}-{DateTime.UtcNow.Ticks}");
        var omarchy = url.TrimEnd('/').Replace(".git", "", StringComparison.Ordinal) == OmarchyRepo;
        string[] clone = omarchy
            ? ["clone", "--quiet", "--depth", "1", "--filter=blob:none", "--sparse", "--", url, tmp]
            : ["clone", "--quiet", "--depth", "1", "--", url, tmp];
        Console.Error.WriteLine($"fetching {url}…");
        try
        {
            if (ThemeGit(clone) != 0) throw new MazapanException($"{url}: couldn't be fetched");
            if (omarchy && ThemeGit(["-C", tmp, "sparse-checkout", "set", "themes"]) != 0) throw new MazapanException($"{url}: its themes couldn't be fetched");
        }
        catch (MazapanException)
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            throw;
        }
        return tmp;
    }

    static int ThemeGit(string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        using var p = System.Diagnostics.Process.Start(psi)!;
        p.StandardOutput.ReadToEndAsync();
        var err = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(300_000)) { try { p.Kill(true); } catch (InvalidOperationException) { } return -1; }
        if (p.ExitCode != 0) Console.Error.WriteLine(err.GetAwaiter().GetResult().Trim());
        return p.ExitCode;
    }

    /// <summary>
    /// The id for a picture's theme: "from-" and its name made an id. One
    /// from the same picture is that theme again; another picture by the
    /// same name gets a number.
    /// </summary>
    static string ThemeIdFor(string stem, string path)
    {
        var slug = System.Text.RegularExpressions.Regex.Replace(stem.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        if (slug.Length > 32) slug = slug[..32].TrimEnd('-');
        if (slug == "") slug = "picture";
        var len = new FileInfo(path).Length;
        for (var n = 1; ; n++)
        {
            var id = "from-" + slug + (n > 1 ? "-" + n : "");
            var dir = Paths.Join(UserThemes(), id);
            if (!Directory.Exists(dir)) return id;
            // Same picture: its copy has the same bytes (a converted one is
            // told by its source's size and name kept in the description).
            var old = Directory.GetFiles(dir, "wallpaper.*").FirstOrDefault();
            if (old != null && new FileInfo(old).Length == len && File.ReadAllBytes(old).AsSpan().SequenceEqual(File.ReadAllBytes(path))) return id;
            if (old != null && Path.GetExtension(old) == ".png" && !FromImage.Shown(path)) return id;
        }
    }

    /// <summary>"sunset_over-the lake" -> "Sunset over the lake".</summary>
    static string Readable(string stem)
    {
        var s = System.Text.RegularExpressions.Regex.Replace(stem, "[-_]+", " ").Trim();
        return s == "" ? "Picture" : char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>themes remove ID: a theme of your own (made from a picture, or put there) goes.</summary>
    static int CmdThemesRemove(string[] args)
    {
        if (args.Length != 1) throw new MazapanException("usage: mazapan themes remove ID");
        var id = args[0];
        if (!Plugins.Plugin.IdPattern().IsMatch(id)) throw new MazapanException($"no theme \"{id}\"");
        var dir = Paths.Join(UserThemes(), id);
        if (!File.Exists(Paths.Join(dir, "theme.toml")))
            throw new MazapanException(Theme.List(ThemeDirs()).Contains(id) ? $"{id} is built in: it can't be removed" : $"no theme \"{id}\"");
        Settings cfg;
        try
        {
            cfg = Settings.Load();
        }
        catch (MazapanException)
        {
            cfg = new Settings();
        }
        if (cfg.Theme == id) throw new MazapanException($"{id} is the theme in use: switch to another first (mazapan apply --theme ID)");
        Directory.Delete(dir, true);
        Console.WriteLine($"removed theme {id}");
        return 0;
    }
}
