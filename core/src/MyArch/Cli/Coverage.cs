using MyArch.Config;
using MyArch.Coverage;
using MyArch.Locale;

namespace MyArch.Cli;

public static partial class Program
{
    static int CmdCoverage(string[] args)
    {
        var fs = new Flags("coverage").Bool("json", "for the theme picker").Parse(args);
        var cfg = Settings.Load();
        var covers = EnabledPlugins(cfg).Select(p => new Covers(p.Id, p.Coverage.Apps, p.Coverage.Toolkits)).ToList();
        var apps = Apps.Report(Apps.Dirs(), Languages.Detect(cfg.Language), covers);
        if (fs.IsSet("json"))
        {
            Console.WriteLine(Apps.Json(apps));
            return 0;
        }
        Console.WriteLine($"The theme reaches {apps.Count(a => a.Plugin != "")} of {apps.Count} apps");
        foreach (var a in apps)
        {
            string mark = Style.Green + "✓" + Style.Reset, by = Style.Dim + a.Plugin + Style.Reset;
            if (a.Plugin == "")
            {
                mark = Style.Amber + "✗" + Style.Reset;
                by = Style.Amber + MissingHint.GetValueOrDefault(a.Toolkit, "") + Style.Reset;
            }
            var name = a.Name;
            var runes = name.EnumerateRunes().ToList();
            if (runes.Count > 30) name = string.Concat(runes.Take(29).Select(r => r.ToString())) + "…";
            // %-30s pads by runes in Go.
            var pad = new string(' ', Math.Max(0, 30 - name.EnumerateRunes().Count()));
            Console.WriteLine($"  {mark} {name}{pad} {ToolkitName.GetValueOrDefault(a.Toolkit, ""),-9} {by}");
        }
        return 0;
    }

    /// <summary>What a plugin for each toolkit would take, for the ones nothing themes.</summary>
    static readonly Dictionary<string, string> MissingHint = new()
    {
        [Toolkit.Terminal] = "no plugin themes the terminal yet",
        [Toolkit.GTK4] = "no plugin themes GTK 4 yet",
        [Toolkit.GTK3] = "no plugin themes GTK 3 yet",
        [Toolkit.Qt6] = "no plugin themes Qt yet (a qt6ct + Kvantum plugin would)",
        [Toolkit.Qt5] = "no plugin themes Qt yet (a qt5ct + Kvantum plugin would)",
        [Toolkit.Electron] = "Electron: only follows dark/light, needs its own plugin",
        [Toolkit.Chromium] = "has its own theming: needs its own plugin",
        [Toolkit.Firefox] = "has its own theming (userChrome.css): needs its own plugin",
        [Toolkit.Flatpak] = "Flatpak: sandboxed, the theme's files don't reach it",
        [Toolkit.Web] = "a web app: it looks as the browser (and the site) make it",
        [Toolkit.Other] = "draws its own UI: needs a plugin for its config",
    };

    static readonly Dictionary<string, string> ToolkitName = new()
    {
        [Toolkit.Terminal] = "terminal", [Toolkit.GTK4] = "GTK 4", [Toolkit.GTK3] = "GTK 3", [Toolkit.Qt6] = "Qt 6",
        [Toolkit.Qt5] = "Qt 5", [Toolkit.Electron] = "Electron", [Toolkit.Chromium] = "Chromium",
        [Toolkit.Firefox] = "Firefox", [Toolkit.Flatpak] = "Flatpak", [Toolkit.Web] = "web app", [Toolkit.Other] = "own UI",
    };
}
