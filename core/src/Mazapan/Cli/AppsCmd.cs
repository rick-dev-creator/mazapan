using System.Diagnostics;
using System.Globalization;
using Mazapan.Applying;
using Mazapan.Config;
using Mazapan.Plugins;
using Mazapan.Store;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// mazapan apps: the apps the catalog knows how to install (catalog/apps.toml)
/// and its profiles, what's installed, and installing or removing them:
/// packages from Arch's official repositories (as root: sudo in a terminal,
/// pkexec from the Apps panel), Flatpaks (as you, from Flathub), sites as
/// apps (webapps), and the mazapan plugins that go with them. Every install
/// and removal is kept (AppsLedger): the timeline shows it, and only what
/// the Apps menu put there is ever taken out.
///
/// For the panel, besides what it says: "step NAME" as it goes, "fail
/// KIND DETAIL" when something didn't go through (cancelled, pacman,
/// flatpak, plugins, webapp, busy).
/// </summary>
public static partial class Program
{
    static string AppsState() => Paths.ExpandHome("~/.local/state/mazapan/apps");

    static int CmdApps(string[] args)
    {
        var sub = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "list";
        var rest = args.Length > 0 && !args[0].StartsWith('-') ? args[1..] : args;
        // Flags before or after the ids.
        rest = [.. rest.Where(a => a.StartsWith('-')), .. rest.Where(a => !a.StartsWith('-'))];
        var fs = new Flags("apps " + sub)
            .Bool("json", "as JSON, for the Apps panel")
            .Bool("gui", "from the Apps panel: the password through polkit, no terminal")
            .Bool("y", "don't ask")
            .Parse(rest);
        var (profiles, apps) = AppsCatalog(sub == "list" && !fs.IsSet("json"));
        var yes = fs.IsSet("y") || fs.IsSet("gui");
        switch (sub)
        {
            case "list": return AppsList(profiles, apps, fs.IsSet("json"));
            case "plan": return AppsPlan(Pick(profiles, apps, fs.Rest), fs.IsSet("json"), remove: false);
            case "plan-remove": return AppsPlan(Pick(profiles, apps, fs.Rest), fs.IsSet("json"), remove: true);
            // The person's own overrides: no root, no lock.
            case "permissions": return AppsPermissions(apps, fs.Rest, fs.IsSet("json"));
            case "permit": return AppsPermit(apps, fs.Rest);
        }
        // One at a time: pacman takes one anyway, and two runs would each
        // record the other's work.
        using var lk = AppsLock();
        return sub switch
        {
            "install" => AppsInstall(Pick(profiles, apps, fs.Rest), fs.IsSet("gui"), yes),
            "remove" => AppsRemove(Pick(profiles, apps, fs.Rest), fs.IsSet("gui"), yes),
            "undo" => AppsUndo(fs.Rest, apps, fs.IsSet("gui"), yes),
            _ => throw new MazapanException("usage: mazapan apps [list|plan|plan-remove|install|remove|undo|permissions|permit] [ID...] [--json] [--gui] [-y]"),
        };
    }

    /// <summary>
    /// The apps one can install: mazapan's catalog, then config.toml's
    /// app_catalogs. One that can't be read is left out (said, when asked).
    /// </summary>
    internal static (List<Profile> Profiles, List<App> Apps) AppsCatalog(bool say)
    {
        var (profiles, apps, problems) = AppCatalog.LoadAll(Paths.Join(Root(), "catalog", "apps.toml"), Settings.Load().AppCatalogs);
        if (say)
            foreach (var p in problems) Console.Error.WriteLine("warning: " + p);
        return (profiles, apps);
    }

    static FileStream AppsLock(bool say = true)
    {
        Directory.CreateDirectory(AppsState());
        try
        {
            return new FileStream(Paths.Join(AppsState(), "lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            if (say) Console.WriteLine("fail busy");
            throw new MazapanException("another install or removal is running: wait for it");
        }
    }

    /// <summary>Apps by id, a profile's by its id.</summary>
    static List<App> Pick(List<Profile> profiles, List<App> apps, List<string> ids)
    {
        if (ids.Count == 0) throw new MazapanException("which apps? (mazapan apps: their ids, or a profile's)");
        var out_ = new List<App>();
        foreach (var id in ids)
        {
            if (apps.FirstOrDefault(a => a.Id == id) is { } a) out_.Add(a);
            else if (profiles.FirstOrDefault(p => p.Id == id) is { } p) out_.AddRange(p.Apps.Select(x => apps.First(a => a.Id == x)));
            else throw new MazapanException($"no app or profile \"{id}\" (mazapan apps)");
        }
        return out_.DistinctBy(a => a.Id).ToList();
    }

    // --- what's there ------------------------------------------------------------------

    sealed class AppsNow
    {
        public HashSet<string> Packages = [], UserFlatpaks = [], SystemFlatpaks = [];
        public Dictionary<string, string> Webapps = []; // url (no trailing /) -> id
        public Settings? Cfg;
        public List<Plugin> Found = [];
        public (HashSet<string> Packages, HashSet<string> Flatpaks, HashSet<string> Webapps) Owned;
        public HashSet<string> Protected = [];
        public HashSet<string> OwnedVendor = [];
        public bool ChromiumBrowser;

        public static AppsNow Read()
        {
            var n = new AppsNow();
            n.Packages = [.. AppsCapture("pacman", "-Qq").Out.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
            if (File.Exists("/usr/bin/flatpak"))
                foreach (var line in AppsCapture("flatpak", "list", "--app", "--columns=application,installation").Out.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var f = line.Split('\t', StringSplitOptions.TrimEntries);
                    if (f.Length < 2) continue;
                    if (f[1] == "user") n.UserFlatpaks.Add(f[0]);
                    else n.SystemFlatpaks.Add(f[0]);
                }
            var apps = Paths.ExpandHome("~/.local/share/applications");
            foreach (var f in Directory.Exists(apps) ? Directory.GetFiles(apps, "mazapan-webapp-*.desktop") : [])
            {
                var url = File.ReadLines(f).FirstOrDefault(l => l.StartsWith("X-Mazapan-WebApp="))?["X-Mazapan-WebApp=".Length..] ?? "";
                if (url != "") n.Webapps[url.TrimEnd('/')] = Paths.Base(f)["mazapan-webapp-".Length..^".desktop".Length];
            }
            try { n.Cfg = Settings.Load(); } catch (MazapanException) { }
            n.Found = Plugin.Discover(PluginDirs()).Plugins;
            var ledger = AppsLedger.List(AppsState());
            n.Owned = AppsLedger.Owned(ledger);
            n.OwnedVendor = AppsLedger.OwnedVendor(ledger);
            // Never removed: the system's, and what an enabled plugin needs.
            n.Protected = [.. AppsLedger.Protected];
            foreach (var p in n.Found.Where(p => n.Cfg != null && n.Cfg.IsOn(p))) n.Protected.UnionWith(p.Pacman);
            n.ChromiumBrowser = new[] { "chromium", "google-chrome", "brave-browser", "brave-bin", "vivaldi", "microsoft-edge-stable-bin", "helium-browser-bin" }.Any(n.Packages.Contains);
            return n;
        }

        public bool Has(App a) => a.Kind switch
        {
            "pacman" => a.Pacman.All(Packages.Contains),
            "flatpak" => UserFlatpaks.Contains(a.Flatpak) || SystemFlatpaks.Contains(a.Flatpak),
            "webapp" => Webapps.ContainsKey(a.Webapp.TrimEnd('/')),
            "vendor" => Vendor.Installed(a.Id) != "",
            _ => Found.FirstOrDefault(p => p.Id == a.Plugin) is { } p && Cfg != null && Cfg.IsOn(p),
        };

        /// <summary>Removable: some of it is the Apps menu's to take out (never the system's, nor what a plugin needs).</summary>
        public bool Removable(App a) => a.Kind switch
        {
            "pacman" => a.Pacman.Any(p => Packages.Contains(p) && Owned.Packages.Contains(p) && !Protected.Contains(p)),
            "flatpak" => UserFlatpaks.Contains(a.Flatpak) && Owned.Flatpaks.Contains(a.Flatpak),
            "webapp" => Webapps.ContainsKey(a.Webapp.TrimEnd('/')) && Owned.Webapps.Contains(a.Webapp.TrimEnd('/')),
            "vendor" => Vendor.Installed(a.Id) != "" && OwnedVendor.Contains(a.Id),
            _ => false, // a plugin: the Plugins panel's
        };
    }

    /// <summary>A command's output, read in C: pacman's words and numbers aren't the person's language here.</summary>
    static (int Code, string Out, string Err) AppsCapture(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        psi.Environment["LC_ALL"] = "C";
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            p.StandardInput.Close(); // a question (which provider?) takes its default
            var err = p.StandardError.ReadToEndAsync();
            var out_ = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode, out_, err.Result);
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            return (127, "", e.Message);
        }
    }

    static int AppsList(List<Profile> profiles, List<App> apps, bool json)
    {
        var now = AppsNow.Read();
        var lang = Locale.Languages.Detect(now.Cfg?.Language ?? "");
        if (!json)
        {
            foreach (var p in profiles)
            {
                var (name, desc) = p.In(lang);
                Console.WriteLine($"{Style.Bold}{name}{Style.Reset} ({p.Id}): {desc}");
                foreach (var id in p.Apps)
                {
                    var a = apps.First(x => x.Id == id);
                    Console.WriteLine($"  {(now.Has(a) ? Style.Green + "✓" : " ")}{Style.Reset} {a.In(lang).Name,-24} {Style.Dim}{a.Id}{Style.Reset}");
                }
            }
            Console.WriteLine("\nmazapan apps install ID…  (an app's id, or a profile's)");
            return 0;
        }
        Console.WriteLine(GoJson.Marshal(new Fields
        {
            { "version", 1 },
            {
                "profiles", profiles.Select(p => new Fields
                {
                    { "id", p.Id }, { "name", p.In(lang).Name }, { "description", p.In(lang).Description },
                    { "glyph", p.Glyph }, { "basic", p.Basic }, { "apps", p.Apps }, { "from", p.From },
                }).ToList()
            },
            {
                "apps", apps.Select(a => new Fields
                {
                    { "id", a.Id }, { "name", a.In(lang).Name }, { "description", a.In(lang).Description },
                    { "category", a.Category }, { "kind", a.Kind }, { "from", a.From }, { "flatpak", a.Flatpak },
                    // A web app's launcher is the one webapps made for it.
                    { "desktop", a.Webapp != "" && now.Webapps.TryGetValue(a.Webapp.TrimEnd('/'), out var wid) ? $"mazapan-webapp-{wid}.desktop"
                        : a.Vendor != "" && now.Has(a) ? Vendor.DesktopName(a.Id) : a.Desktop },
                    { "installed", now.Has(a) },
                    { "removable", now.Removable(a) },
                    { "plugin", a.Plugin },
                }).ToList()
            },
        }));
        return 0;
    }

    // --- the plan -----------------------------------------------------------------------

    sealed class AppsPlanned
    {
        public List<App> Apps = [];       // what will be installed
        public List<string> Review = [];  // plugins to add through the Plugins panel (what they can do, approved)
        public List<(string Name, string Version, long Download, long Installed)> Packages = [];
        public List<string> Targets = []; // the packages asked for (not their dependencies)
        public List<string> Flatpaks = [];
        public List<(string Name, string Url)> Webapps = [];
        public List<(App App, VendorRelease Release)> Vendor = []; // from their makers
        public List<string> Enable = [];  // mazapan plugins turned on
        public long Download, Installed;
    }

    static AppsPlanned PlanInstall(List<App> apps, AppsNow now, string lang)
    {
        var p = new AppsPlanned();
        foreach (var a in apps.Where(a => !now.Has(a)))
        {
            if (a.Plugin != "")
            {
                var pl = now.Found.FirstOrDefault(x => x.Id == a.Plugin);
                // Not here yet (a catalog's), or a hardware one: the Plugins panel's, where it shows what it can do.
                if (pl == null || pl.Hardware != null) { p.Review.Add(a.Plugin); continue; }
                p.Enable.Add(pl.Id);
            }
            p.Apps.Add(a);
            p.Targets.AddRange(a.Pacman.Where(x => !now.Packages.Contains(x)));
            if (a.Flatpak != "") p.Flatpaks.Add(a.Flatpak);
            if (a.Webapp != "") p.Webapps.Add((a.In(lang).Name, a.Webapp));
            if (a.Vendor != "")
            {
                try { p.Vendor.Add((a, Mazapan.Store.Vendor.Latest(a.Vendor))); }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
                {
                    throw new MazapanException($"{a.In(lang).Name}: its maker can't be reached ({e.Message})");
                }
            }
        }
        // Flatpaks need flatpak; sites as apps, the webapps plugin and a
        // Chromium-based browser (only those open a site as an app).
        if (p.Flatpaks.Count > 0 && !now.Packages.Contains("flatpak"))
        {
            p.Targets.Add("flatpak");
        }
        if (p.Webapps.Count > 0)
        {
            if (now.Found.FirstOrDefault(x => x.Id == "webapps") is { } wp && (now.Cfg == null || !now.Cfg.IsOn(wp))) p.Enable.Add("webapps");
            if (!now.ChromiumBrowser) p.Targets.Add("chromium");
        }
        // What goes with them: their plugins; an app asked for that's already
        // here (installed some other way) gets its own too (its theme, its setup).
        foreach (var a in p.Apps.Concat(apps.Where(now.Has)))
            foreach (var id in a.Plugins)
                if (now.Found.FirstOrDefault(x => x.Id == id) is { } pl && pl.Hardware == null && (now.Cfg == null || !now.Cfg.IsOn(pl)) && !p.Enable.Contains(id))
                    p.Enable.Add(id);
        p.Targets = p.Targets.Distinct().ToList();
        // What another package there already gives (nodejs by nodejs-lts-*):
        // left, not swapped (pacman would ask to remove it, and --noconfirm says no).
        if (p.Targets.Count > 0 && AppsCapture("pacman", ["-T", "--", .. p.Targets]) is { Code: 0 or 127 } dt)
        {
            var missing = dt.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            p.Targets = p.Targets.Where(missing.Contains).ToList();
        }
        // Never synced (installed offline, from the ISO): nothing to plan
        // against yet; the install syncs and upgrades first (AppsInstall).
        if (p.Targets.Count > 0 && NeverSynced())
            foreach (var t in p.Targets) p.Packages.Add((t, "?", 0, 0));
        else if (p.Targets.Count > 0)
        {
            var (code, out_, err) = AppsCapture("pacman", ["-Sp", "--needed", "--print-format", "%n %v", "--", .. p.Targets]);
            if (code != 0) throw new MazapanException("pacman: " + (err.Trim() != "" ? err.Trim() : "can't plan it") + " (mazapan update first?)");
            // Only package lines: a question (which provider?) prints its choices too.
            var lines = out_.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Split(' ')).Where(f => f.Length == 2 && AppCatalog.IsPackage(f[0])).ToList();
            var sizes = PackageSizes(lines.Select(f => f[0]).ToList());
            foreach (var f in lines)
            {
                var (d, i) = sizes.GetValueOrDefault(f[0]);
                p.Packages.Add((f[0], f[1], d, i));
            }
            p.Download = p.Packages.Sum(x => x.Download);
            p.Installed = p.Packages.Sum(x => x.Installed);
        }
        p.Download += p.Vendor.Sum(v => v.Release.Size);
        return p;
    }

    /// <summary>Download and installed size of packages, from the repositories' databases.</summary>
    static Dictionary<string, (long, long)> PackageSizes(List<string> names)
    {
        var out_ = new Dictionary<string, (long, long)>();
        if (names.Count == 0) return out_;
        var (_, text, _) = AppsCapture("pacman", ["-Si", "--", .. names]);
        string name = "";
        long dl = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var k = line[..colon].Trim();
            var v = line[(colon + 1)..].Trim();
            if (k == "Name") name = v;
            else if (k == "Download Size") dl = Bytes(v);
            else if (k == "Installed Size" && name != "") out_[name] = (dl, Bytes(v));
        }
        return out_;
    }

    static long Bytes(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)) return 0;
        return (long)(n * parts[1] switch { "KiB" => 1024.0, "MiB" => 1048576.0, "GiB" => 1073741824.0, _ => 1.0 });
    }

    sealed class AppsRemoval
    {
        public List<App> Apps = [];
        public List<string> Packages = [];      // the ones asked to go
        public List<string> Removed = [];       // what pacman -Rs takes out with them
        public List<(string Name, string Why)> Kept = [];
        public List<string> Flatpaks = [];
        public List<string> Webapps = [];       // their ids
        public List<App> Vendor = [];           // from their makers
    }

    /// <summary>
    /// What removing them takes out: only what the Apps menu put there, never
    /// what the system or an enabled plugin needs, nor what something else
    /// still requires.
    /// </summary>
    static AppsRemoval PlanRemove(List<App> apps, AppsNow now)
    {
        var r = new AppsRemoval();
        foreach (var a in apps.Where(now.Removable))
        {
            r.Apps.Add(a);
            foreach (var pkg in a.Pacman.Where(now.Packages.Contains))
            {
                if (now.Protected.Contains(pkg)) r.Kept.Add((pkg, "protected"));
                else if (!now.Owned.Packages.Contains(pkg)) r.Kept.Add((pkg, "yours"));
                else r.Packages.Add(pkg);
            }
            if (a.Flatpak != "") r.Flatpaks.Add(a.Flatpak);
            if (a.Webapp != "") r.Webapps.Add(now.Webapps[a.Webapp.TrimEnd('/')]);
            if (a.Vendor != "") r.Vendor.Add(a);
        }
        r.Packages = r.Packages.Distinct().ToList();
        // What pacman would take out, dependencies included; one that another
        // package still needs stays (pacman says which).
        for (var tries = 0; tries < 20 && r.Packages.Count > 0; tries++)
        {
            var (code, out_, err) = AppsCapture("pacman", ["-Rsp", "--print-format", "%n", "--", .. r.Packages]);
            if (code == 0)
            {
                r.Removed = out_.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(AppCatalog.IsPackage).ToList();
                break;
            }
            // "removing X breaks dependency 'X' required by Y"
            var m = System.Text.RegularExpressions.Regex.Match(err, @"removing (\S+) breaks dependency '[^']*' required by (\S+)");
            if (!m.Success || !r.Packages.Contains(m.Groups[1].Value))
                throw new MazapanException("pacman: " + err.Trim());
            r.Packages.Remove(m.Groups[1].Value);
            r.Kept.Add((m.Groups[1].Value, "needed by " + m.Groups[2].Value));
        }
        return r;
    }

    static int AppsPlan(List<App> apps, bool json, bool remove)
    {
        var now = AppsNow.Read();
        var lang = Locale.Languages.Detect(now.Cfg?.Language ?? "");
        if (remove)
        {
            var r = PlanRemove(apps, now);
            if (json)
            {
                Console.WriteLine(GoJson.Marshal(new Fields
                {
                    { "apps", r.Apps.Select(a => a.Id).ToList() }, { "packages", r.Removed.Count > 0 ? r.Removed : r.Packages },
                    { "kept", r.Kept.Select(k => new Fields { { "name", k.Name }, { "why", k.Why } }).ToList() },
                    { "flatpaks", r.Flatpaks }, { "webapps", r.Webapps }, { "vendor", r.Vendor.Select(a => a.Id).ToList() },
                }));
                return 0;
            }
            PrintRemovePlan(r, lang);
            return 0;
        }
        var plan = PlanInstall(apps, now, lang);
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "apps", plan.Apps.Select(a => a.Id).ToList() },
                { "packages", plan.Packages.Select(x => new Fields { { "name", x.Name }, { "version", x.Version }, { "download", x.Download }, { "installed", x.Installed } }).ToList() },
                { "download", plan.Download }, { "installed", plan.Installed },
                { "flatpaks", plan.Flatpaks },
                { "webapps", plan.Webapps.Select(w => new Fields { { "name", w.Name }, { "url", w.Url } }).ToList() },
                { "vendor", plan.Vendor.Select(v => new Fields { { "id", v.App.Id }, { "name", v.App.In(lang).Name }, { "version", v.Release.Version }, { "download", v.Release.Size } }).ToList() },
                { "enable", plan.Enable }, { "review", plan.Review },
            }));
            return 0;
        }
        PrintInstallPlan(plan, lang);
        return 0;
    }

    static void PrintInstallPlan(AppsPlanned plan, string lang)
    {
        if (plan.Apps.Count > 0) Console.WriteLine($"install {string.Join(", ", plan.Apps.Select(a => a.In(lang).Name))}");
        if (plan.Packages.Count > 0)
            Console.WriteLine($"  {plan.Packages.Count} packages: {Mb(plan.Download)} to download, {Mb(plan.Installed)} on disk");
        foreach (var x in plan.Packages) Console.WriteLine($"    {x.Name} {Style.Dim}{x.Version}{Style.Reset}");
        foreach (var f in plan.Flatpaks) Console.WriteLine($"  flatpak  {f} (Flathub)");
        foreach (var w in plan.Webapps) Console.WriteLine($"  web app  {w.Name} ({w.Url})");
        foreach (var (a, rel) in plan.Vendor) Console.WriteLine($"  from its maker  {a.In(lang).Name} {rel.Version}" + (rel.Size > 0 ? $" ({Mazapan.Store.Vendor.Size(rel.Size)})" : ""));
        foreach (var e in plan.Enable) Console.WriteLine($"  plugin   {e} turned on");
        foreach (var r in plan.Review) Console.WriteLine($"  {Style.Amber}plugin   {r}: add it from the Plugins panel (it shows what it can do){Style.Reset}");
    }

    static void PrintRemovePlan(AppsRemoval r, string lang)
    {
        Console.WriteLine($"remove {string.Join(", ", r.Apps.Select(a => a.In(lang).Name))}");
        foreach (var p in r.Removed.Count > 0 ? r.Removed : r.Packages) Console.WriteLine($"  package  {p}");
        foreach (var (name, why) in r.Kept) Console.WriteLine($"  {Style.Dim}kept     {name} ({why}){Style.Reset}");
        foreach (var f in r.Flatpaks) Console.WriteLine($"  flatpak  {f}");
        foreach (var w in r.Webapps) Console.WriteLine($"  web app  {w}");
        foreach (var v in r.Vendor) Console.WriteLine($"  from its maker  {v.In(lang).Name}");
    }

    static string Mb(long b) => b >= 1073741824 ? $"{b / 1073741824.0:0.0} GB" : $"{b / 1048576.0:0} MB";

    // --- doing it --------------------------------------------------------------------------

    /// <summary>As root: pkexec (the polkit agent asks) from the panel, sudo in a terminal.</summary>
    static int AsRootRun(bool gui, params string[] args)
    {
        if (!gui) return AsRoot.Sudo(args);
        var psi = new ProcessStartInfo("pkexec") { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        return p.ExitCode;
    }

    /// <summary>
    /// The default apps again (plugin default-apps' script, when it's on):
    /// an app just installed may be the first of its kind, one removed may
    /// have been the default.
    /// </summary>
    static void DefaultApps()
    {
        var script = Paths.ExpandHome("~/.local/share/mazapan/bin/default-apps");
        if (File.Exists(script)) AppsCapture("sh", script);
    }

    /// <summary>No repository database yet: a system installed offline, from the ISO.</summary>
    /// <summary>
    /// A repository whose database pacman never fetched (installed offline,
    /// or Mazapán's added since): pacman -S refuses every package until a
    /// -Sy, so the install is a -Syu then.
    /// </summary>
    static bool NeverSynced()
    {
        var (code, out_, _) = AppsCapture("pacman-conf", "--repo-list");
        var repos = code == 0 ? out_.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : ["core"];
        return repos.Any(r => !File.Exists($"/var/lib/pacman/sync/{r}.db"));
    }

    /// <summary>pacman as root; a password refused or the dialog closed is "cancelled", not a failure.</summary>
    static void Pacman(bool gui, string[] args, string what)
    {
        var code = AsRootRun(gui, ["pacman", .. args]);
        if (code == 0) return;
        // pkexec: 126 the person said no (or closed it), 127 not authorized.
        if (gui && code is 126 or 127)
        {
            Console.WriteLine("fail cancelled");
            throw new MazapanException("cancelled: nothing " + what);
        }
        Console.WriteLine($"fail pacman {code}");
        throw new MazapanException($"pacman stopped (exit {code}): nothing {what}");
    }

    static int AppsRun(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Console.Error.WriteLine($"{file}: {e.Message}");
            return 127;
        }
    }

    /// <summary>A step, said so the panel can show it (lines starting "step ").</summary>
    static void Step(string what) => Console.WriteLine("step " + what);

    static int AppsInstall(List<App> apps, bool gui, bool yes)
    {
        var now = AppsNow.Read();
        var lang = Locale.Languages.Detect(now.Cfg?.Language ?? "");
        var plan = PlanInstall(apps, now, lang);
        foreach (var r in plan.Review) Console.WriteLine($"{r}: add it from the Plugins panel (it shows what it can do first)");
        if (plan.Apps.Count == 0 && plan.Enable.Count == 0)
        {
            Console.WriteLine(plan.Review.Count > 0 ? "nothing else to install" : "already installed");
            return 0;
        }
        PrintInstallPlan(plan, lang);
        if (!yes)
        {
            if (!IsTerminal(0)) throw new MazapanException("no terminal to ask on: install with -y");
            if (!Confirm("Install them?")) return 0;
        }
        var tx = new AppsTx { Action = "install" };
        var failed = new List<string>();
        var okApps = new HashSet<string>();
        if (plan.Targets.Count > 0)
        {
            Step("packages");
            // A system that never synced gets the repositories and the updates
            // with them: -Syu, never -Sy alone (Arch has no partial upgrades).
            Pacman(gui, [NeverSynced() ? "-Syu" : "-S", "--needed", "--noconfirm", .. gui ? new[] { "--noprogressbar" } : [], "--", .. plan.Targets], "installed");
            tx.Packages = plan.Targets;
        }
        foreach (var a in plan.Apps.Where(a => a.Kind == "pacman")) okApps.Add(a.Id);
        if (plan.Flatpaks.Count > 0)
        {
            Step("flatpaks");
            if (AppsRun("flatpak", "remote-add", "--user", "--if-not-exists", "flathub", "https://dl.flathub.org/repo/flathub.flatpakrepo") != 0)
                failed.Add("flatpak");
            else
                foreach (var f in plan.Flatpaks)
                {
                    if (AppsRun("flatpak", "install", "--user", "--noninteractive", "-y", "flathub", f) == 0)
                    {
                        tx.Flatpaks.Add(f);
                        okApps.Add(plan.Apps.First(a => a.Flatpak == f).Id);
                    }
                    else failed.Add("flatpak " + f);
                }
        }
        if (plan.Webapps.Count > 0 && plan.Enable.Contains("webapps"))
        {
            // The helper first: the web apps need it.
            Step("plugins");
            if (!EnablePlugins(["webapps"])) failed.Add("plugins webapps");
            else tx.Plugins.Add("webapps");
        }
        if (plan.Webapps.Count > 0)
        {
            Step("webapps");
            var helper = Paths.ExpandHome("~/.local/share/mazapan/bin/webapp");
            foreach (var (name, url) in plan.Webapps)
            {
                if (AppsRun("sh", helper, "add", name, url) == 0)
                {
                    tx.Webapps.Add(url);
                    okApps.Add(plan.Apps.First(a => a.Webapp == url).Id);
                }
                else failed.Add("webapp " + name);
            }
        }
        if (plan.Vendor.Count > 0)
        {
            Step("vendor");
            foreach (var (a, rel) in plan.Vendor)
            {
                try
                {
                    Mazapan.Store.Vendor.Install(a.Id, Mazapan.Store.Vendor.MakerOf(a.Vendor), rel, a.In(lang).Name);
                    tx.Vendor.Add(a.Id);
                    okApps.Add(a.Id);
                }
                catch (Exception e) when (e is MazapanException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine(e.Message);
                    failed.Add("vendor " + a.In(lang).Name);
                }
            }
        }
        var rest = plan.Enable.Where(e => !tx.Plugins.Contains(e)).ToList();
        if (rest.Count > 0)
        {
            Step("plugins");
            if (EnablePlugins(rest))
            {
                tx.Plugins.AddRange(rest);
                foreach (var a in plan.Apps.Where(a => a.Plugin != "" && rest.Contains(a.Plugin))) okApps.Add(a.Id);
            }
            else failed.Add("plugins " + string.Join(",", rest));
        }
        var done = plan.Apps.Where(a => okApps.Contains(a.Id)).ToList();
        tx.Apps = done.Select(a => a.Id).ToList();
        tx.Names = done.Select(a => a.In(lang).Name).ToList();
        if (!tx.Empty) AppsLedger.Save(AppsState(), tx);
        DefaultApps();
        foreach (var f in failed) Console.WriteLine("fail " + f);
        if (failed.Count > 0) throw new MazapanException("not everything: " + string.Join("; ", failed));
        Console.WriteLine(tx.Names.Count > 0 ? "installed " + string.Join(", ", tx.Names) : "already installed; set up: " + string.Join(", ", tx.Plugins));
        return 0;
    }

    /// <summary>Plugins turned on (an apply): whether it went through.</summary>
    static bool EnablePlugins(List<string> ids)
    {
        try
        {
            return CmdApply([.. ids.Select(e => "--enable=" + e)]) == 0;
        }
        catch (Exception e) when (e is MazapanException or ConflictException)
        {
            Console.Error.WriteLine(e.Message);
            return false;
        }
    }

    static int AppsRemove(List<App> apps, bool gui, bool yes, List<string>? plugins = null)
    {
        var now = AppsNow.Read();
        var lang = Locale.Languages.Detect(now.Cfg?.Language ?? "");
        var r = PlanRemove(apps, now);
        if (r.Apps.Count == 0)
        {
            Console.WriteLine("nothing of that was installed from Apps (a plugin: the Plugins panel)");
            return 0;
        }
        PrintRemovePlan(r, lang);
        if (!yes)
        {
            if (!IsTerminal(0)) throw new MazapanException("no terminal to ask on: remove with -y");
            if (!Confirm("Remove them?")) return 0;
        }
        var tx = new AppsTx { Action = "remove" };
        var failed = new List<string>();
        var okApps = r.Apps.Where(a => a.Kind == "pacman").Select(a => a.Id).ToHashSet();
        if (r.Packages.Count > 0)
        {
            Step("packages");
            // -Rs: with the dependencies nothing else needs; never -c.
            Pacman(gui, ["-Rs", "--noconfirm", .. gui ? new[] { "--noprogressbar" } : [], "--", .. r.Packages], "removed");
            tx.Packages = r.Packages;
        }
        if (r.Flatpaks.Count > 0)
        {
            Step("flatpaks");
            foreach (var f in r.Flatpaks)
            {
                if (AppsRun("flatpak", "uninstall", "--user", "--noninteractive", "-y", f) == 0)
                {
                    tx.Flatpaks.Add(f);
                    okApps.Add(r.Apps.First(a => a.Flatpak == f).Id);
                }
                else failed.Add("flatpak " + f);
            }
            // The runtimes nothing uses any more.
            AppsRun("flatpak", "uninstall", "--user", "--unused", "--noninteractive", "-y");
        }
        foreach (var w in r.Webapps)
        {
            var a = r.Apps.First(x => x.Webapp != "" && now.Webapps.GetValueOrDefault(x.Webapp.TrimEnd('/')) == w);
            if (AppsRun("sh", Paths.ExpandHome("~/.local/share/mazapan/bin/webapp"), "remove", w) == 0)
            {
                tx.Webapps.Add(a.Webapp);
                okApps.Add(a.Id);
            }
            else failed.Add("webapp " + a.In(lang).Name);
        }
        foreach (var a in r.Vendor)
        {
            try
            {
                Mazapan.Store.Vendor.Remove(a.Id, Mazapan.Store.Vendor.MakerOf(a.Vendor));
                tx.Vendor.Add(a.Id);
                okApps.Add(a.Id);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or MazapanException)
            {
                Console.Error.WriteLine(e.Message);
                failed.Add("vendor " + a.In(lang).Name);
            }
        }
        // The plugins that came with them (an undo says which), if nothing
        // else installed still needs them.
        if (plugins is { Count: > 0 })
        {
            var after = AppsNow.Read();
            var catalogApps = AppsCatalog(false).Apps;
            var stillWanted = catalogApps.Where(after.Has).SelectMany(a => a.Plugins.Concat(a.Plugin != "" ? [a.Plugin] : [])).ToHashSet();
            if (after.Webapps.Count > 0) stillWanted.Add("webapps");
            var off = plugins.Where(p => !stillWanted.Contains(p) && after.Found.FirstOrDefault(x => x.Id == p) is { } pl && after.Cfg != null && after.Cfg.IsOn(pl)).ToList();
            if (off.Count > 0)
            {
                Step("plugins");
                try
                {
                    if (CmdApply([.. off.Select(e => "--disable=" + e)]) == 0) tx.Plugins = off;
                    else failed.Add("plugins " + string.Join(",", off));
                }
                catch (Exception e) when (e is MazapanException or ConflictException)
                {
                    failed.Add("plugins " + e.Message);
                }
            }
        }
        var done = r.Apps.Where(a => okApps.Contains(a.Id)).ToList();
        tx.Apps = done.Select(a => a.Id).ToList();
        tx.Names = done.Select(a => a.In(lang).Name).ToList();
        if (!tx.Empty) AppsLedger.Save(AppsState(), tx);
        DefaultApps();
        foreach (var f in failed) Console.WriteLine("fail " + f);
        if (failed.Count > 0) throw new MazapanException("not everything: " + string.Join("; ", failed));
        Console.WriteLine("removed " + string.Join(", ", tx.Names));
        return 0;
    }

    /// <summary>
    /// An install undone: what it put there that's still there goes (and the
    /// plugins it turned on, if nothing else needs them). A removal undone:
    /// those apps installed again.
    /// </summary>
    static int AppsUndo(List<string> ids, List<App> apps, bool gui, bool yes)
    {
        if (ids.Count != 1) throw new MazapanException("usage: mazapan apps undo ID");
        var tx = AppsLedger.List(AppsState()).FirstOrDefault(t => t.Id == ids[0]) ?? throw new MazapanException($"no apps change {ids[0]} (mazapan timeline)");
        var those = apps.Where(a => tx.Apps.Contains(a.Id)).ToList();
        if (those.Count == 0) throw new MazapanException("the apps it was about aren't in the catalog any more: nothing to undo");
        return tx.Action == "install" ? AppsRemove(those, gui, yes, tx.Plugins) : AppsInstall(those, gui, yes);
    }

    /// <summary>Whether undoing it would do anything now: its apps still (or no longer) there as it left them.</summary>
    static bool AppsUndoable(AppsTx tx, List<App> apps, AppsNow now)
    {
        var those = apps.Where(a => tx.Apps.Contains(a.Id)).ToList();
        if (those.Count == 0) return false;
        return tx.Action == "install" ? those.Any(now.Removable) : those.Any(a => !now.Has(a));
    }
}
