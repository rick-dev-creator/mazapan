using System.Diagnostics;
using System.Text.RegularExpressions;
using MyArch.Applying;
using MyArch.Config;
using MyArch.Locale;
using MyArch.Plugins;
using MyArch.Rendering;
using MyArch.Themes;
using MyArch.Util;

namespace MyArch.Cli;

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
            return (changes, orphans, owned);
        }

        /// <summary>Write applies the plan, records ownership, runs reload commands and prints what happened.</summary>
        public void Write(List<Change> changes, List<string> orphans, Owned owned, bool adopt)
        {
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
            throw new MyArchException($"no theme selected; available: {string.Join(", ", Theme.List(ThemeDirs()))}\n  myarch apply --theme <id>");
        var t = Theme.Load(ThemeDirs(), cfg.Theme);
        if (cfg.Accent != "")
        {
            try
            {
                t.WithAccent(cfg.Accent);
            }
            catch (FormatException e)
            {
                throw new MyArchException("accent: " + e.Message);
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
            Console.WriteLine($"  {Style.Dim}unreadable: not plain JSON (comments?), left as it is; set what myarch would by hand{Style.Reset}");
    }

    static (string Plugin, string Key) SplitKey(string s, string flag)
    {
        var dot = s.IndexOf('.');
        if (dot <= 0 || dot == s.Length - 1) throw new MyArchException($"{flag} {s}: use plugin.key");
        return (s[..dot], s[(dot + 1)..]);
    }

    /// <summary>plugin.key=value: the value as TOML (true, 480, 0.5, "text", ["a"]), or else as text.</summary>
    static (string Plugin, string Key, object Value) ParseSet(string s)
    {
        var eq = s.IndexOf('=');
        if (eq < 0) throw new MyArchException($"--set {s}: use plugin.key=value");
        var (plugin, key) = SplitKey(s[..eq], "--set");
        var raw = s[(eq + 1)..];
        object value = raw;
        try
        {
            value = Toml.Parse("v = " + raw + "\n", "--set")["v"]!;
        }
        catch (Exception e) when (e is MyArchException or Tomlyn.TomlException) { } // not a TOML value: text
        return (plugin, key, value);
    }

    /// <summary>PrintDiffs shows, as unified diffs, what writing the plan puts on disk.</summary>
    static void PrintDiffs(List<Change> changes, List<string> orphans, Owned owned, bool adopt)
    {
        foreach (var c in changes)
        {
            if (c.State is not (State.New or State.Changed) && !(c.State == State.Conflict && adopt)) continue;
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

    static int CmdApply(string[] args)
    {
        var fs = new Flags("apply")
            .String("theme", "switch to this theme (saved in config)")
            .String("accent", "use this accent color, #rrggbb; \"theme\" for the theme's own (saved in config)")
            .Bool("dry-run", "show what would change, write nothing")
            .Bool("adopt", "back up and take over files myarch didn't write")
            .Bool("diff", "show exactly what would be written, as a unified diff")
            .Bool("changes-only", "list only the files that change")
            .List("set", "set a plugin's setting in config.toml: plugin.key=value (a TOML value: true, 480, \"text\")")
            .List("reset", "back to the plugin's default: plugin.key")
            .List("enable", "enable a plugin")
            .List("disable", "disable a plugin")
            .Parse(args);
        var themeId = fs.Get("theme");
        var accent = fs.Get("accent");
        if (accent != "" && accent != "theme" && !AccentPattern().IsMatch(accent))
            throw new MyArchException($"--accent {GoFormat.Quote(accent)}: use #rrggbb, or \"theme\" for the theme's own");
        var edits = new List<string>(); // what changes config.toml, for undo --list
        var sets = fs.All("set").Select(ParseSet).ToList();
        var resets = fs.All("reset").Select(r => SplitKey(r, "--reset")).ToList();
        var s = LoadWith(c =>
        {
            if (themeId != "") c.Theme = themeId;
            if (accent == "theme") c.Accent = "";
            else if (accent != "") c.Accent = accent.ToLowerInvariant();
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
                c.Disabled.RemoveAll(d => d == id);
                edits.Add("enable " + id);
            }
            foreach (var id in fs.All("disable"))
            {
                if (!c.Disabled.Contains(id)) c.Disabled.Add(id);
                edits.Add("disable " + id);
            }
        });
        // What's changed is checked even for a plugin that stays disabled: a
        // wrong key there would break the day it's enabled.
        var found = Plugin.Discover(PluginDirs()).Plugins;
        foreach (var id in fs.All("enable").Concat(fs.All("disable")).Concat(sets.Select(x => x.Plugin)).Concat(resets.Select(x => x.Plugin)))
            if (found.All(p => p.Id != id)) throw new MyArchException($"no plugin \"{id}\"");
        foreach (var p in found.Where(p => sets.Any(x => x.Plugin == p.Id))) p.Resolve(s.Cfg.For(p.Id));
        foreach (var (plugin, key) in resets)
            if (!found.First(p => p.Id == plugin).Settings.ContainsKey(key))
                throw new MyArchException($"--reset {plugin}.{key}: {plugin} has no setting {key}");
        WarnMissingPackages(s.Plugins);
        // One apply at a time, from the plan to the end: what it compares
        // against must still be there when it writes.
        using var lk = fs.IsSet("dry-run") ? null : ApplyLock.Take();
        var (changes, orphans, owned) = s.Plan();
        Console.WriteLine($"theme {s.Theme.Id}, language {s.Lang}, {s.Plugins.Count} plugins");
        // With the diff, the plan lists only what changes: the diff is what to read.
        PrintPlan(changes, orphans, fs.IsSet("diff") || fs.IsSet("changes-only"));
        if (fs.IsSet("diff")) PrintDiffs(changes, orphans, owned, fs.IsSet("adopt"));
        if (fs.IsSet("dry-run")) return 0;
        // Every apply can be undone: what it touches is kept first.
        var configChanged = themeId != "" || accent != "" || edits.Count > 0;
        var what = themeId != "" || accent != "" ? $"theme {s.Theme.Id}" + (s.Cfg.Accent != "" ? $", accent {s.Cfg.Accent}" : "") : "";
        if (edits.Count > 0) what = (what == "" ? "" : what + "; ") + string.Join(", ", edits);
        if (what == "") what = "apply";
        var snap = Snapshots.Begin(changes, orphans, fs.IsSet("adopt"), Settings.Path, what, configOnly: configChanged);
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
        if (snap != null)
        {
            Snapshots.Finish(snap, Settings.Path);
            if (Directory.Exists(snap.Dir())) Console.WriteLine($"undo with: myarch undo (id {snap.ID})");
        }
        return 0;
    }

    static void WarnMissingPackages(List<Plugin> plugins)
    {
        if (!File.Exists("/usr/bin/pacman")) return;
        foreach (var p in plugins)
            foreach (var pkg in p.Pacman)
                if (Run("pacman", "-Q", pkg) != 0)
                    Console.Error.WriteLine($"warning: plugin {p.Id} needs package {pkg} (not installed)");
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
