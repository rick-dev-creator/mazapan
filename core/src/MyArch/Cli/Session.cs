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

    [GeneratedRegex(@"^#[0-9a-fA-F]{6}\z")]
    private static partial Regex AccentPattern();

    static int CmdApply(string[] args)
    {
        var fs = new Flags("apply")
            .String("theme", "switch to this theme (saved in config)")
            .String("accent", "use this accent color, #rrggbb; \"theme\" for the theme's own (saved in config)")
            .Bool("dry-run", "show what would change, write nothing")
            .Bool("adopt", "back up and take over files myarch didn't write")
            .Parse(args);
        var themeId = fs.Get("theme");
        var accent = fs.Get("accent");
        if (accent != "" && accent != "theme" && !AccentPattern().IsMatch(accent))
            throw new MyArchException($"--accent {GoFormat.Quote(accent)}: use #rrggbb, or \"theme\" for the theme's own");
        var s = LoadWith(c =>
        {
            if (themeId != "") c.Theme = themeId;
            if (accent == "theme") c.Accent = "";
            else if (accent != "") c.Accent = accent.ToLowerInvariant();
        });
        WarnMissingPackages(s.Plugins);
        var (changes, orphans, owned) = s.Plan();
        Console.WriteLine($"theme {s.Theme.Id}, language {s.Lang}, {s.Plugins.Count} plugins");
        PrintPlan(changes, orphans, false);
        if (fs.IsSet("dry-run")) return 0;
        s.Write(changes, orphans, owned, fs.IsSet("adopt"));
        if (themeId != "" || accent != "") s.Cfg.Save();
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
