using System.Diagnostics;
using System.Text.RegularExpressions;
using Mazapan.Applying;
using Mazapan.Config;
using Mazapan.Locale;
using Mazapan.Plugins;
using Mazapan.Rendering;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Cli;

public static partial class Program
{
    /// <summary>
    /// Session is everything loaded and rendered for one run: config, theme,
    /// the enabled plugins, the language, and what they produce.
    /// </summary>
    sealed class Session
    {
        public required Settings Cfg;
        public required Theme Theme;
        public required List<Plugin> Plugins;
        public required string Lang;
        public required Output Out;

        /// <summary>Plan compares the rendered files with the disk.</summary>
        public (List<Change> Changes, List<string> Orphans, Owned Owned) Plan()
        {
            var owned = Apply.LoadOwned(Apply.StatePath());
            var (changes, orphans) = Apply.Plan(Out.Files, owned);
            // A plugin that can't be read renders nothing: its files would look
            // like orphans and go. Kept until it reads again (or is turned off).
            if (BrokenNotOff.Count > 0 && orphans.Count > 0)
            {
                Console.Error.WriteLine($"warning: {orphans.Count} file(s) no plugin wrote are kept while {string.Join(", ", BrokenNotOff)} can't be read");
                orphans = [];
            }
            return (changes, orphans, owned);
        }

        /// <summary>Write applies the plan, records ownership, runs reload commands and prints what happened.</summary>
        public void Write(List<Change> changes, List<string> orphans, Owned owned, bool adopt)
        {
            // Files as root wait for apply --system (SystemPhase); one already
            // as it would be is mazapan's all the same (no sudo needed to say so).
            foreach (var c in changes.Where(c => c.File.System && c.State == State.Unchanged))
                owned[c.Path] = Apply.Proposed(c, owned).Sum;
            changes = changes.Where(c => !c.File.System).ToList();
            orphans = orphans.Where(o => !AsRoot.IsSystem(o)).ToList();
            Result res;
            try
            {
                res = Apply.Execute(changes, orphans, owned, adopt);
            }
            catch (ConflictException)
            {
                throw;
            }
            catch (Exception)
            {
                // Record whatever was written, even after a partial failure.
                try
                {
                    owned.Save(Apply.StatePath());
                }
                catch (Exception) { }
                throw;
            }
            owned.Save(Apply.StatePath());
            StampVersion();
            foreach (var (p, bak) in res.Backups.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                Console.WriteLine($"backed up {Tilde(p)} -> {Paths.Base(bak)}");
            foreach (var p in res.Removed) Console.WriteLine($"removed {Tilde(p)} (no plugin generates it anymore)");
            foreach (var p in res.Kept) Console.WriteLine($"left {Tilde(p)} in place: it was edited, no longer managed");
            foreach (var p in res.Shared) Console.WriteLine($"left {Tilde(p)} in place: it's its app's file too, no longer managed");
            foreach (var (cmd, e) in Apply.Reload(res.Written))
                Console.Error.WriteLine($"warning: reload {GoFormat.Quote(cmd)} failed: {e}");
            Console.WriteLine($"{res.Written.Count} written");
        }
    }

    /// <summary>Load reads the config, the theme and the enabled plugins, and renders everything.</summary>
    static Session Load() => LoadWith(null);

    /// <summary>
    /// LoadWith is Load with config changes that aren't saved yet (switching
    /// theme or accent), so they're only saved once they render.
    /// </summary>
    static Session LoadWith(System.Action<Settings>? change)
    {
        var cfg = Settings.Load();
        change?.Invoke(cfg);
        if (cfg.Theme == "")
            throw new MazapanException($"no theme selected; available: {string.Join(", ", Theme.List(ThemeDirs()))}\n  mazapan apply --theme <id>");
        var t = Theme.Load(ThemeDirs(), cfg.Theme);
        // The person's fonts over the theme's (each checked: it goes into code).
        if (cfg.FontUI != "") t.Font.UI = FontName(cfg.FontUI, "font_ui");
        if (cfg.FontMono != "") t.Font.Mono = FontName(cfg.FontMono, "font_mono");
        if (cfg.FontSize > 0) t.Font.Size = cfg.FontSize is >= 6 and <= 32 ? cfg.FontSize : throw new MazapanException("font_size: from 6 to 32");
        if (cfg.Accent != "")
        {
            try
            {
                t.WithAccent(cfg.Accent);
            }
            catch (FormatException e)
            {
                throw new MazapanException("accent: " + e.Message);
            }
        }
        var plugins = EnabledPlugins(cfg);
        CheckOverrides(cfg);
        var lang = Languages.Detect(cfg.Language);
        var out_ = Renderer.All(plugins, t, cfg.For, lang);
        return new Session { Cfg = cfg, Theme = t, Plugins = plugins, Lang = lang, Out = out_ };
    }

    static void PrintPlan(List<Change> changes, List<string> orphans, bool onlyChanges)
    {
        foreach (var c in changes)
        {
            if (onlyChanges && c.State == State.Unchanged) continue;
            Console.WriteLine($"  {c.State.Name(),-10} {c.Plugin,-16} {Tilde(c.Path)}");
        }
        foreach (var o in orphans) Console.WriteLine($"  {"orphan",-10} {"-",-16} {Tilde(o)}");
        if (changes.Any(c => c.State == State.Busy))
            Console.WriteLine($"  {Style.Dim}busy: its app is running and would write its own copy back; close it and apply again{Style.Reset}");
        if (changes.Any(c => c.State == State.Unreadable))
            Console.WriteLine($"  {Style.Dim}unreadable: not plain JSON (comments?), left as it is; set what mazapan would by hand{Style.Reset}");
    }

    static (string Plugin, string Key) SplitKey(string s, string flag)
    {
        var dot = s.IndexOf('.');
        if (dot <= 0 || dot == s.Length - 1) throw new MazapanException($"{flag} {s}: use plugin.key");
        return (s[..dot], s[(dot + 1)..]);
    }

    /// <summary>plugin.key=value: the value as TOML (true, 480, 0.5, "text", ["a"]), or else as text.</summary>
    static (string Plugin, string Key, object Value) ParseSet(string s)
    {
        var eq = s.IndexOf('=');
        if (eq < 0) throw new MazapanException($"--set {s}: use plugin.key=value");
        var (plugin, key) = SplitKey(s[..eq], "--set");
        var raw = s[(eq + 1)..];
        object value = raw;
        try
        {
            value = Toml.Parse("v = " + raw + "\n", "--set")["v"]!;
        }
        catch (Exception e) when (e is MazapanException or Tomlyn.TomlException) { } // not a TOML value: text
        return (plugin, key, value);
    }

    /// <summary>
    /// SystemPhase does, with sudo, what --system asked for, once the person
    /// has seen it: packages first (a driver before the files that load it),
    /// then the plugins' checks that must pass before (kernel headers for a
    /// module built on install), then the system files, then their reloads,
    /// and the reloads of the files it removes. Returns the packages it
    /// installed (undo takes them out again).
    /// </summary>
    static List<string> SystemPhase(Session s, List<Change> changes, List<string> orphans, List<string> packages, Owned owned, bool adopt, bool yes)
    {
        var conflicts = changes.Where(c => c.State == State.Conflict).Select(c => c.Path).ToList();
        if (conflicts.Count > 0 && !adopt) throw new ConflictException(conflicts);
        var reloads = SystemState.Reloads();
        var removals = orphans.Where(o => Apply.ReadOrNull(o) is { } b && Apply.Sum(b) == owned.Get(o)).ToList();

        // Everything it will do as root, in full, before the first sudo.
        Header("As root, with sudo");
        foreach (var p in packages) Console.WriteLine($"  install  {p}");
        foreach (var c in changes)
        {
            var before = Apply.ReadOrNull(c.Path) is { } b ? Files.Utf8.GetString(b) : "";
            Console.Write(Diff.Unified(before, Files.Utf8.GetString(Apply.Proposed(c, owned).Content),
                before == "" ? "/dev/null" : c.Path, c.Path));
        }
        foreach (var o in removals) Console.WriteLine($"  remove   {o}");
        var commands = changes.Select(c => c.Reload).Concat(removals.Select(o => reloads.GetValueOrDefault(o, "")))
            .Where(r => r != "").Distinct().ToList();
        foreach (var cmd in commands) Console.WriteLine($"  run      {cmd}");
        if (!yes)
        {
            if (!IsTerminal(0)) throw new MazapanException("no terminal to ask on: read the list above and run with --system -y");
            if (!Confirm("Do this as root?")) return [];
        }

        AsRoot.Install(packages);
        if (packages.Count > 0) Console.WriteLine($"  installed {string.Join(", ", packages)}");
        foreach (var c in s.Out.Checks.Where(c => c.Before && s.Plugins.Any(p => p.Id == c.Plugin && changes.Any(ch => ch.Plugin == p.Id))))
        {
            var r = Health.Checks.RunOne(c);
            if (!r.OK && !r.Skipped)
                throw new MazapanException($"{c.Name} ({c.Plugin}): it must hold before its system files are written\n{r.Output.TrimEnd()}");
        }
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var written = new List<Change>();
        try
        {
            foreach (var c in changes)
            {
                if (c.State == State.Conflict && Apply.ReadOrNull(c.Path) is { } mine)
                {
                    AsRoot.Write(c.Path + ".mazapan-bak-" + stamp, mine); // someone's edit: kept
                    Console.WriteLine($"  backed up {c.Path} -> {Paths.Base(c.Path)}.mazapan-bak-{stamp}");
                }
                var (content, sum) = Apply.Proposed(c, owned);
                AsRoot.Write(c.Path, content);
                owned[c.Path] = sum;
                reloads[c.Path] = c.Reload;
                written.Add(c);
                Console.WriteLine($"  wrote {c.Path}");
            }
            foreach (var o in orphans)
            {
                if (removals.Contains(o))
                {
                    AsRoot.Remove(o);
                    Console.WriteLine($"  removed {o} (no plugin generates it anymore)");
                }
                else if (Apply.ReadOrNull(o) != null)
                    Console.WriteLine($"  left {o} in place: it was edited, no longer managed");
                owned.Remove(o);
            }
        }
        finally
        {
            owned.Save(Apply.StatePath());
            var keep = reloads.Where(kv => owned.ContainsKey(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
            SystemState.Save(keep);
        }
        foreach (var cmd in commands)
            if (AsRoot.Run(cmd) is { } err) Console.Error.WriteLine($"warning: reload {GoFormat.Quote(cmd)} (as root) failed: {err}");
        var reboot = written.Where(c => c.File.Reboot).Select(c => c.Path).ToList();
        if (reboot.Count > 0) Console.WriteLine($"{Style.Amber}Reboot to use: {string.Join(", ", reboot)}{Style.Reset}");
        return packages;
    }

    /// <summary>PrintDiffs shows, as unified diffs, what writing the plan puts on disk.</summary>
    static void PrintDiffs(List<Change> changes, List<string> orphans, Owned owned, bool adopt)
    {
        foreach (var c in changes)
        {
            if (c.State is not (State.New or State.Changed) && !(c.State == State.Conflict && adopt)) continue;
            if (c.File.System) Console.WriteLine($"# as root, with apply --system:");
            var before = Apply.ReadOrNull(c.Path) is { } b ? Files.Utf8.GetString(b) : "";
            var after = Files.Utf8.GetString(Apply.Proposed(c, owned).Content);
            Console.Write(Diff.Unified(before, after, c.State == State.New ? "/dev/null" : Tilde(c.Path), Tilde(c.Path)));
        }
        foreach (var o in orphans)
        {
            if (Apply.ReadOrNull(o) is not { } b) continue;
            var after = Apply.ProposedOrphan(o, owned);
            Console.Write(Diff.Unified(Files.Utf8.GetString(b), after == null ? "" : Files.Utf8.GetString(after),
                Tilde(o), after == null ? "/dev/null" : Tilde(o)));
        }
    }

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}\z")]
    private static partial Regex AccentPattern();

    /// <summary>
    /// The version of Mazapán this account's desktop was last written by: a
    /// login after Mazapán changed (an update through pacman by hand, or
    /// another account's) applies it (apply --if-updated), so nothing needs
    /// migrations or a hook in pacman.
    /// </summary>
    static string VersionStamp => Paths.ExpandHome("~/.local/state/mazapan/applied-version");

    static void StampVersion()
    {
        try
        {
            Directory.CreateDirectory(Paths.Dir(VersionStamp));
            Files.WriteAtomic(VersionStamp, Version + "\n");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // only means one more apply
    }

    static bool UpToDate() => File.Exists(VersionStamp) && File.ReadAllText(VersionStamp).Trim() == Version
        && File.Exists(Apply.StatePath());

    // A font family's name, as fc-list writes them: letters, digits, spaces
    // and a few signs; never a quote (it's put into QML, Lua, ini, CSS).
    [GeneratedRegex(@"^[\p{L}\p{N} ._+&-]{1,64}\z")]
    private static partial Regex FontPattern();

    static string FontName(string name, string what) =>
        FontPattern().IsMatch(name) ? name : throw new MazapanException($"{what} {GoFormat.Quote(name)}: a font family's name (letters, digits, spaces)");

    static int CmdApply(string[] args)
    {
        var fs = new Flags("apply")
            .String("theme", "switch to this theme (saved in config)")
            .String("accent", "use this accent color, #rrggbb; \"theme\" for the theme's own (saved in config)")
            .String("language", "the desktop's language (es, pt_BR); \"system\" for the system's (saved in config)")
            .String("font-ui", "the font for the desktop's text (a family, as fc-list says); \"theme\" for the theme's (saved in config)")
            .String("font-mono", "the monospace font (terminals, code); \"theme\" for the theme's (saved in config)")
            .String("font-size", "the text's size in points (6 to 32); \"theme\" for the theme's (saved in config)")
            .Bool("dry-run", "show what would change, write nothing")
            .Bool("adopt", "back up and take over files mazapan didn't write")
            .Bool("diff", "show exactly what would be written, as a unified diff")
            .Bool("changes-only", "list only the files that change")
            .Bool("system", "also write the system files (/etc) and install the packages hardware plugins need, with sudo")
            .Bool("y", "with --system: don't ask (everything is still listed first)")
            .Bool("agent", "what an agent may do: no hardware plugins, nothing as root (the MCP server's applies)")
            .List("set", "set a plugin's setting in config.toml: plugin.key=value (a TOML value: true, 480, \"text\")")
            .List("reset", "back to the plugin's default: plugin.key")
            .List("enable", "enable a plugin")
            .List("disable", "disable a plugin")
            .Bool("if-updated", "only if this Mazapán isn't the one that last wrote the desktop (at login)")
            .Parse(args);
        if (fs.IsSet("if-updated") && UpToDate()) return 0;
        var themeId = fs.Get("theme");
        var accent = fs.Get("accent");
        var language = fs.Get("language");
        var fontUI = fs.Get("font-ui");
        var fontMono = fs.Get("font-mono");
        var fontSize = fs.Get("font-size");
        if (fontUI is not ("" or "theme")) FontName(fontUI, "--font-ui");
        if (fontMono is not ("" or "theme")) FontName(fontMono, "--font-mono");
        double size = 0;
        if (fontSize is not ("" or "theme") && !(double.TryParse(fontSize, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out size) && size is >= 6 and <= 32))
            throw new MazapanException($"--font-size {GoFormat.Quote(fontSize)}: points from 6 to 32, or \"theme\"");
        if (language != "" && language != "system" && !System.Text.RegularExpressions.Regex.IsMatch(language, @"^[a-z]{2,3}(_[A-Z]{2})?\z"))
            throw new MazapanException($"--language {GoFormat.Quote(language)}: a language code (es, pt_BR), or \"system\"");
        if (accent != "" && accent != "theme" && !AccentPattern().IsMatch(accent))
            throw new MazapanException($"--accent {GoFormat.Quote(accent)}: use #rrggbb, or \"theme\" for the theme's own");
        var edits = new List<string>(); // what changes config.toml, for undo --list
        var found = Plugin.Discover(PluginDirs()).Plugins;
        foreach (var id in fs.All("enable").Concat(fs.All("disable")))
            if (found.All(p => p.Id != id)) throw new MazapanException($"no plugin \"{id}\"");
        if (fs.IsSet("agent"))
        {
            // Hardware plugins write as root, on the person's say: an agent
            // neither turns them on nor changes what they write.
            if (fs.IsSet("system")) throw new MazapanException("an agent doesn't apply as root: the person runs mazapan apply --system");
            foreach (var id in fs.All("enable").Concat(fs.All("disable")).Concat(fs.All("set").Select(x => x.Split('.')[0])).Concat(fs.All("reset").Select(x => x.Split('.')[0])))
                if (found.FirstOrDefault(p => p.Id == id) is { } hp && (hp.Hardware != null || hp.Targets.Any(t => t.System)))
                    throw new MazapanException($"{id} is a hardware plugin: turning it on or off, or changing it, is the person's (mazapan hardware)");
        }
        var sets = fs.All("set").Select(ParseSet).ToList();
        var resets = fs.All("reset").Select(r => SplitKey(r, "--reset")).ToList();
        // One apply at a time, from reading config.toml to the end: two at
        // once (a theme picked while an undo runs) would each save its own
        // config.toml over the other's change; and what the plan compares
        // against must still be there when it writes.
        using var lk = fs.IsSet("dry-run") ? null : ApplyLock.Take();
        var s = LoadWith(c =>
        {
            if (themeId != "") c.Theme = themeId;
            if (language == "system") c.Language = "";
            else if (language != "") c.Language = language;
            if (language != "") edits.Add("language " + (language == "system" ? "the system's" : language));
            if (accent == "theme") c.Accent = "";
            else if (accent != "") c.Accent = accent.ToLowerInvariant();
            if (fontUI != "") { c.FontUI = fontUI == "theme" ? "" : fontUI; edits.Add("font " + (fontUI == "theme" ? "the theme's" : fontUI)); }
            if (fontMono != "") { c.FontMono = fontMono == "theme" ? "" : fontMono; edits.Add("monospace font " + (fontMono == "theme" ? "the theme's" : fontMono)); }
            if (fontSize != "") { c.FontSize = fontSize == "theme" ? 0 : size; edits.Add("text size " + (fontSize == "theme" ? "the theme's" : fontSize)); }
            foreach (var (plugin, key, value) in sets)
            {
                if (!c.Plugins.TryGetValue(plugin, out var m)) c.Plugins[plugin] = m = new(StringComparer.Ordinal);
                m[key] = value;
                edits.Add($"{plugin}.{key}");
            }
            foreach (var (plugin, key) in resets)
            {
                if (c.Plugins.TryGetValue(plugin, out var m) && m.Remove(key) && m.Count == 0) c.Plugins.Remove(plugin);
                edits.Add($"{plugin}.{key} reset");
            }
            foreach (var id in fs.All("enable"))
            {
                c.TurnOn(found.First(p => p.Id == id));
                edits.Add("enable " + id);
            }
            foreach (var id in fs.All("disable"))
            {
                c.TurnOff(found.First(p => p.Id == id));
                edits.Add("disable " + id);
            }
        });
        // What's changed is checked even for a plugin that stays disabled: a
        // wrong key there would break the day it's enabled.
        foreach (var id in sets.Select(x => x.Plugin).Concat(resets.Select(x => x.Plugin)))
            if (found.All(p => p.Id != id)) throw new MazapanException($"no plugin \"{id}\"");
        foreach (var p in found.Where(p => sets.Any(x => x.Plugin == p.Id))) p.Resolve(s.Cfg.For(p.Id));
        foreach (var (plugin, key) in resets)
            if (!found.First(p => p.Id == plugin).Settings.ContainsKey(key))
                throw new MazapanException($"--reset {plugin}.{key}: {plugin} has no setting {key}");
        var (changes, orphans, owned) = s.Plan();
        Console.WriteLine($"theme {s.Theme.Id}, language {s.Lang}, {s.Plugins.Count} plugins");
        // With the diff, the plan lists only what changes: the diff is what to read.
        PrintPlan(changes, orphans, fs.IsSet("diff") || fs.IsSet("changes-only"));
        if (fs.IsSet("diff")) PrintDiffs(changes, orphans, owned, fs.IsSet("adopt"));
        // What needs root: system files, and packages plugins need.
        var sysChanges = changes.Where(c => c.File.System && c.State is State.New or State.Changed or State.Conflict).ToList();
        var sysOrphans = orphans.Where(AsRoot.IsSystem).ToList();
        // Hardware and optional plugins' packages are installed (the person
        // turned them on; mazapan's package doesn't bring them); the others'
        // are only said, as they always were (the package depends on them).
        var packages = AsRoot.Missing(s.Plugins.Where(p => p.OffByDefault).SelectMany(p => p.Pacman));
        foreach (var p in s.Plugins.Where(p => !p.OffByDefault))
            foreach (var pkg in AsRoot.Missing(p.Pacman))
                Console.Error.WriteLine($"warning: plugin {p.Id} needs package {pkg} (not installed)");
        foreach (var pkg in packages)
            Console.WriteLine($"  {"install",-10} {string.Join(",", s.Plugins.Where(p => p.Pacman.Contains(pkg)).Select(p => p.Id)),-16} {pkg}");
        var rootWork = sysChanges.Count + sysOrphans.Count + packages.Count;
        if (fs.IsSet("dry-run")) return 0;
        // Every apply can be undone: what it touches is kept first.
        var configChanged = themeId != "" || accent != "" || edits.Count > 0;
        var what = themeId != "" || accent != "" ? $"theme {s.Theme.Id}" + (s.Cfg.Accent != "" ? $", accent {s.Cfg.Accent}" : "") : "";
        if (edits.Count > 0) what = (what == "" ? "" : what + "; ") + string.Join(", ", edits);
        if (what == "") what = "apply";
        // Packages alone are a change too: undo takes them out.
        var snap = noSnapshots ? null : Snapshots.Begin(changes, orphans, fs.IsSet("adopt"), Settings.Path, what,
            configOnly: configChanged || (fs.IsSet("system") && rootWork > 0));
        // The machine's very first apply (an install, a first setup): the
        // welcome opens at the next login (the welcome plugin reads this).
        // Written first: the shell reloads as the files are written.
        if (!File.Exists(Apply.StatePath()))
        {
            var welcome = Paths.ExpandHome("~/.local/state/mazapan/welcome.json");
            if (!File.Exists(welcome))
            {
                Directory.CreateDirectory(Paths.Dir(welcome));
                Files.WriteAtomic(welcome, "{\"step\":\"language\",\"done\":false}\n");
            }
        }
        try
        {
            s.Write(changes, orphans, owned, fs.IsSet("adopt"));
            if (configChanged) s.Cfg.Save();
        }
        catch (ConflictException)
        {
            Snapshots.Discard(snap); // nothing was written
            throw;
        }
        catch (Exception)
        {
            // Written half way: kept, so what was written can still be undone.
            if (snap != null) Snapshots.Finish(snap, Settings.Path);
            throw;
        }
        if (rootWork > 0 && fs.IsSet("system"))
        {
            try
            {
                var installed = SystemPhase(s, sysChanges, sysOrphans, packages, owned, fs.IsSet("adopt"), fs.IsSet("y"));
                if (snap != null) snap.Packages = installed;
            }
            finally
            {
                if (snap != null) Snapshots.Finish(snap, Settings.Path);
            }
            snap = null;
        }
        else if (rootWork > 0)
            Console.WriteLine($"{Style.Amber}{Plural(sysChanges.Count + sysOrphans.Count, "system file", "system files")} and " +
                $"{Plural(packages.Count, "package", "packages")} wait for: mazapan apply --system (with sudo){Style.Reset}");
        if (snap != null)
        {
            Snapshots.Finish(snap, Settings.Path);
            if (Directory.Exists(snap.Dir())) Console.WriteLine($"undo with: mazapan undo (id {snap.ID})");
        }
        return 0;
    }

    /// <summary>Runs a command quietly and returns its exit status (-1: it didn't start).</summary>
    static int Run(string cmd, params string[] args)
    {
        var psi = new ProcessStartInfo(cmd) { RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Close();
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return -1;
        }
    }
}
