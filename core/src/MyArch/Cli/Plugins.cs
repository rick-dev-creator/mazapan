using MyArch.Config;
using MyArch.Install;
using MyArch.Plugins;
using MyArch.Util;

namespace MyArch.Cli;

public static partial class Program
{
    /// <summary>
    /// EnabledPlugins are the plugins to apply. It refuses when one doesn't
    /// load, needs a plugin that isn't there, or is a plugin from git that
    /// isn't what plugins.lock says (moved, edited, or needing more than was
    /// approved): applying would run what nobody agreed to.
    /// </summary>
    static List<Plugin> EnabledPlugins(Settings cfg)
    {
        var (all, broken) = Plugin.Discover(PluginDirs());
        var lck = PluginsLock.Load();
        var out_ = new List<Plugin>();
        var failed = new List<Plugin>();
        var problems = new List<string>();
        foreach (var (id, err) in broken)
            if (!cfg.IsDisabled(id)) problems.Add(err);
        var found = new HashSet<string>();
        foreach (var p in all)
        {
            found.Add(p.Id);
            // Off, or hardware that isn't this machine's: not applied.
            if (!cfg.IsOn(p) || !Applies(p)) continue;
            try
            {
                Trusted(lck, p);
                out_.Add(p);
            }
            catch (MyArchException e)
            {
                problems.Add(e.Message);
                failed.Add(p);
            }
        }
        foreach (var e in lck.Plugins)
            if (!found.Contains(e.Id) && !broken.ContainsKey(e.Id) && !cfg.IsDisabled(e.Id))
                problems.Add(e.Id + " is in plugins.lock but not installed (run: myarch plugins sync)");
        // Those that failed still count as there: their dependents' problem is
        // theirs, not a missing plugin.
        problems.AddRange(Dependencies.Unmet([.. out_, .. failed], all, broken).Select(p => p.Text));
        if (problems.Count > 0) throw new MyArchException("plugins:\n  " + string.Join("\n  ", problems));
        return out_;
    }

    /// <summary>Applies: a plugin for every machine, or a hardware plugin whose rules this machine meets.</summary>
    static bool Applies(Plugin p) => p.Hardware == null || p.Hardware.MatchesIdentity(Hardware.ThisMachine.Get());

    /// <summary>A plugin's state, as listed.</summary>
    static string PluginState(Settings cfg, Plugin p) => (cfg.IsOn(p), Applies(p)) switch
    {
        (true, true) => "enabled",
        (true, false) => "on, not this machine",
        (false, _) when p.Hardware != null => "off",
        _ => "disabled",
    };

    /// <summary>Trusted: built-ins and your own plugins are; one from git must be what plugins.lock says.</summary>
    internal static void Trusted(PluginsLock lck, Plugin p)
    {
        var e = lck.Get(p.Id);
        var fromGit = Paths.Dir(p.Dir) == Git.Dir;
        if (e != null && !fromGit)
            throw new MyArchException($"{p.Id} is in plugins.lock but not installed (run: myarch plugins sync)");
        // A link is a plugin someone is writing (plugins dev): theirs, in
        // their own repository.
        if (e == null && fromGit && IsCheckout(p.Dir) && !IsLink(p.Dir))
            throw new MyArchException($"{p.Id} is a git checkout that isn't in plugins.lock; add it with myarch plugins add, or delete {Tilde(p.Dir)}");
        if (e != null) Git.Verify(p.Dir, e, p);
    }

    static bool IsCheckout(string dir) => Paths.Exists(Paths.Join(dir, ".git"));

    static bool IsLink(string path) => new FileInfo(path).LinkTarget != null;

    /// <summary>LockedEntry is p's plugins.lock entry when p is the plugin installed from git; a plugin elsewhere with the same id isn't.</summary>
    static Entry? LockedEntry(PluginsLock lck, Plugin p)
    {
        var e = lck.Get(p.Id);
        return e == null || Paths.Dir(p.Dir) != Git.Dir ? null : e;
    }

    /// <summary>CheckOverrides rejects [plugins.&lt;id&gt;] sections for plugins that don't exist, so a misspelled id doesn't silently do nothing.</summary>
    static void CheckOverrides(Settings cfg)
    {
        var (all, broken) = Plugin.Discover(PluginDirs());
        foreach (var id in cfg.Plugins.Keys)
            if (!broken.ContainsKey(id) && all.All(p => p.Id != id))
                throw new MyArchException($"{Settings.Path}: [plugins.{id}]: no such plugin");
    }

    static int CmdPlugins(string[] args)
    {
        var yes = false;
        var json = false;
        var refresh = false;
        var kind = "";
        var dir = "";
        var commit = "";
        var rest = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-y" or "--yes") yes = true;
            else if (a is "--json") json = true;
            else if (a is "--refresh") refresh = true;
            else if (a is "--kind" or "--dir" or "--commit")
            {
                if (i + 1 >= args.Length) throw new MyArchException($"{a}: what?");
                if (a == "--kind") kind = args[++i];
                else if (a == "--dir") dir = args[++i];
                else commit = args[++i];
            }
            else if (a.StartsWith("--kind=")) kind = a[7..];
            else if (a.StartsWith("--dir=")) dir = a[6..];
            else rest.Add(a);
        }
        var sub = "list";
        if (rest.Count > 0)
        {
            sub = rest[0];
            rest.RemoveAt(0);
        }
        void Need(int min, int max, string what)
        {
            if (rest.Count < min || (max >= 0 && rest.Count > max))
                throw new MyArchException($"usage: myarch plugins {sub} {what}");
        }
        switch (sub)
        {
            case "list":
                Need(0, 0, "");
                PluginsList();
                break;
            case "show":
                Need(1, 1, "ID [--json]");
                if (json) PluginsShowJson(rest[0]);
                else PluginsShow(rest[0]);
                break;
            case "enable":
                Need(1, -1, "ID...");
                PluginsEnable(rest);
                break;
            case "disable":
                Need(1, -1, "ID...");
                PluginsDisable(rest);
                break;
            case "add":
                Need(1, 1, "ID|URL[#REF] [-y] [--commit SHA]");
                PluginsAdd(rest[0], yes, commit);
                break;
            case "update":
                PluginsUpdate(rest, yes);
                break;
            case "remove":
                Need(1, -1, "ID...");
                PluginsRemove(rest);
                break;
            case "catalog":
                Need(0, 0, "[--json] [--refresh]");
                PluginsCatalog(json, refresh, []);
                break;
            case "search":
                Need(1, -1, "TERM... [--json]");
                PluginsCatalog(json, refresh, [.. rest]);
                break;
            case "preview":
                Need(1, 1, "ID|URL[#REF] [--json]");
                PluginsPreview(rest[0], json);
                break;
            case "sync":
                Need(0, 0, "");
                PluginsSync();
                break;
            case "new":
                Need(1, 1, "ID [--kind bar|panel|window|theme|tools] [--dir DIR]");
                PluginsNew(rest[0], kind, dir);
                break;
            case "check":
                Need(0, 1, "[ID|DIR]");
                return PluginsCheck(rest.Count > 0 ? rest[0] : "");
            case "dev":
                Need(0, 1, "[ID|DIR]");
                PluginsDev(rest.Count > 0 ? rest[0] : "");
                break;
            case "fork":
                Need(1, 2, "ID [NEW]");
                PluginsFork(rest[0], rest.Count > 1 ? rest[1] : "");
                break;
            case "diff":
                Need(1, 1, "ID");
                PluginsDiff(rest[0]);
                break;
            default:
                throw new MyArchException($"plugins {sub}: list, show, catalog, search, preview, enable, disable, add, update, remove, sync, new, check, dev, fork or diff");
        }
        return 0;
    }

    /// <summary>Catalog is everything the plugin commands look at.</summary>
    sealed class Catalog(Settings cfg, PluginsLock lck, List<Plugin> all, SortedDictionary<string, string> broken)
    {
        public Settings Cfg => cfg;
        public PluginsLock Lock => lck;
        public List<Plugin> All { get; set; } = all;
        public SortedDictionary<string, string> Broken => broken;

        public static Catalog Load()
        {
            var cfg = Settings.Load();
            var lck = PluginsLock.Load();
            var (all, broken) = Plugin.Discover(PluginDirs());
            return new(cfg, lck, all, broken);
        }

        public Plugin? Find(string id) => All.FirstOrDefault(p => p.Id == id);

        /// <summary>Enabled are the plugins not disabled, besides skip.</summary>
        public List<Plugin> Enabled(params IEnumerable<string> skip)
        {
            var s = skip.ToHashSet();
            return All.Where(p => cfg.IsOn(p) && Applies(p) && !s.Contains(p.Id)).ToList();
        }

        /// <summary>Origin: built-in, local (a folder someone put in the plugin folder), or the git source it was added from.</summary>
        public string Origin(Plugin p)
        {
            if (LockedEntry(lck, p) is { } e) return "git " + e.Source + " @ " + e.Commit[..10];
            return Paths.Dir(p.Dir) == Git.Dir ? "local" : "built-in";
        }
    }

    static void PluginsList()
    {
        var c = Catalog.Load();
        foreach (var p in c.All)
        {
            var state = PluginState(c.Cfg, p);
            Console.WriteLine($"{p.Id,-18} {p.Meta.Version,-8} {state,-9} {p.Meta.Description}");
            var o = c.Origin(p);
            if (o != "built-in") Console.WriteLine($"{"",-18} from {o}");
        }
        foreach (var (id, err) in c.Broken)
            Console.WriteLine($"{id,-18} {"?",-8} {"broken",-9} {err}");
        try
        {
            EnabledPlugins(c.Cfg);
        }
        catch (MyArchException)
        {
            Console.WriteLine();
            throw;
        }
    }

    static void PluginsShow(string id)
    {
        var c = Catalog.Load();
        var p = c.Find(id);
        if (p == null)
        {
            if (c.Broken.TryGetValue(id, out var err)) throw new MyArchException(err);
            throw new MyArchException($"no plugin \"{id}\" (myarch plugins lists them)");
        }
        var state = PluginState(c.Cfg, p);
        Console.WriteLine($"{Style.Bold}{p.Meta.Name}{Style.Reset} {p.Meta.Version}, {state}, {c.Origin(p)}");
        if (p.Meta.Description != "") Console.WriteLine(p.Meta.Description);
        var e = LockedEntry(c.Lock, p);
        if (e != null && e.Ref != "") Console.WriteLine($"follows {e.Ref}");
        Console.WriteLine(Tilde(p.Dir));
        if (p.Meta.Requires.Count > 0) Console.WriteLine($"\nrequires: {string.Join(", ", p.Meta.Requires)}");
        var d = Dependencies.Dependents(id, c.All);
        if (d.Count > 0) Console.WriteLine($"required by: {string.Join(", ", d)}");
        var settings = p.Resolve(c.Cfg.For(id));
        if (settings.Count > 0)
        {
            Console.WriteLine("\nsettings ([plugins." + id + "] in config.toml):");
            foreach (var k in settings.Keys.Order(StringComparer.Ordinal))
            {
                var mark = c.Cfg.For(id)?.ContainsKey(k) == true ? "  (set in config.toml)" : "";
                Console.WriteLine($"  {k} = {GoValue(settings[k])}{mark}");
            }
        }
        Console.WriteLine("\nit can:");
        foreach (var cap in p.Capabilities())
        {
            var mark = e != null && !e.Approved.Contains(cap) ? "  (not approved)" : "";
            Console.WriteLine($"  - {cap}{mark}");
        }
        try
        {
            Trusted(c.Lock, p);
        }
        catch (MyArchException)
        {
            Console.WriteLine();
            throw;
        }
    }

    /// <summary>
    /// A plugin as the Plugins panel shows it: what it is, its README, what it
    /// can do, its settings with what they are and their values now.
    /// </summary>
    static void PluginsShowJson(string id)
    {
        var c = Catalog.Load();
        var p = c.Find(id) ?? throw new MyArchException(c.Broken.GetValueOrDefault(id) ?? $"no plugin \"{id}\"");
        var f = Detail(c, p, LockedEntry(c.Lock, p));
        Console.WriteLine(GoJson.Marshal(f));
    }

    /// <summary>The README in the language (README.es_MX.md, README.es.md), or else README.md.</summary>
    static string? Readme(string dir, string lang)
    {
        if (!Directory.Exists(dir)) return null;
        var files = Directory.GetFiles(dir);
        var lc = lang.Split('_')[0];
        foreach (var name in new[] { $"README.{lang}.md", $"README.{lc}.md", "README.md" })
            if (files.FirstOrDefault(f => Paths.Base(f).Equals(name, StringComparison.OrdinalIgnoreCase)) is { } f) return f;
        return null;
    }

    /// <summary>A plugin, installed or only fetched to look at (preview), as the Plugins panel's page shows it.</summary>
    static Fields Detail(Catalog c, Plugin p, Entry? e, string previewCommit = "")
    {
        var lang = Locale.Languages.Detect(c.Cfg.Language);
        var cat = Locale.Catalog.Load(p.Id, p.Dir, lang);
        // What the plugin doesn't translate, its catalog entry may: the entry
        // for the repository it's from (not another catalog's words for a
        // built-in plugin, or for another plugin of that id).
        (string Name, string Description) listed = e == null ? ("", "") : CatalogIndex.Load(Catalogs(c.Cfg), false).Entries
            .FirstOrDefault(x => x.Id == p.Id && SameSource(x.Source, e.Source))?.TranslatedTo(lang) ?? (Name: "", Description: "");
        var installed = previewCommit == "";
        IReadOnlyDictionary<string, object> values;
        var settingsError = "";
        try
        {
            values = installed ? p.Resolve(c.Cfg.For(p.Id)) : p.Settings;
        }
        catch (MyArchException ex)
        {
            // config.toml has something it doesn't take: shown as it is, with
            // what's wrong (apply refuses it).
            var v = new Dictionary<string, object>(p.Settings);
            foreach (var (k, x) in c.Cfg.For(p.Id) ?? new Dictionary<string, object>())
                if (v.ContainsKey(k)) v[k] = x;
            values = v;
            settingsError = ex.Message;
        }
        var readme = Readme(p.Dir, lang);
        var origin = installed ? c.Origin(p) : "available";
        return new Fields
        {
            { "id", p.Id },
            { "name", cat.TryT("plugin.name") ?? NonEmpty(listed.Name) ?? p.Meta.Name },
            { "version", p.Meta.Version },
            { "description", cat.TryT("plugin.description") ?? NonEmpty(listed.Description) ?? p.Meta.Description },
            { "categories", p.CategoriesOrGuess() },
            { "origin", origin.StartsWith("git ") ? "git" : origin },
            { "source", e?.Source ?? "" },
            { "commit", installed ? e?.Commit ?? "" : previewCommit },
            { "enabled", installed && c.Cfg.IsOn(p) },
            { "applies", Applies(p) },
            { "hardware", p.Hardware?.Describe() ?? "" },
            { "requires", p.Meta.Requires },
            { "required_by", installed ? Dependencies.Dependents(p.Id, c.All) : [] },
            { "readme", readme != null ? File.ReadAllText(readme) : "" },
            { "settings_error", settingsError },
            {
                "capabilities", p.Capabilities().Select(cap => new Fields
                {
                    { "text", cap },
                    { "kind", Plugin.KindOf(cap).Key },
                    { "subject", Plugin.KindOf(cap).Subject },
                    { "risk", cap.StartsWith("full access, as root") || cap.StartsWith("runs as root") ? "root" : cap.StartsWith("full access") || cap.StartsWith("runs ") ? "high" : "low" },
                    { "approved", e == null ? installed : e.Approved.Contains(cap) },
                }).ToList()
            },
            {
                "settings", p.Settings.Keys.Order(StringComparer.Ordinal).Select(k =>
                {
                    var info = p.SettingsInfo.GetValueOrDefault(k) ?? new SettingInfo { Kind = SettingInfo.KindOf(k, p.Settings[k]), Label = SettingInfo.LabelOf(k) };
                    var f = new Fields
                    {
                        { "key", k },
                        { "label", cat.TryT($"setting.{k}") ?? info.Label },
                        { "description", cat.TryT($"setting.{k}.help") ?? info.Description },
                        { "kind", info.Kind },
                        { "default", JsonValue(p.Settings[k]) },
                        { "value", JsonValue(values[k]) },
                        { "set", installed && c.Cfg.For(p.Id)?.ContainsKey(k) == true },
                    };
                    if (info.Choices.Count > 0) f.Add("choices", info.Choices.Select(JsonValue).ToList());
                    if (info.Min != null) f.Add("min", info.Min.Value);
                    if (info.Max != null) f.Add("max", info.Max.Value);
                    if (info.Step != null) f.Add("step", info.Step.Value);
                    return f;
                }).ToList()
            },
        };
    }

    /// <summary>The catalogs to read: myarch's own, then config.toml's.</summary>
    static List<string> Catalogs(Config.Settings cfg) => [Paths.Join(Root(), "catalog", "index.toml"), .. cfg.Catalogs];

    /// <summary>
    /// Every plugin one can see: built in, installed, and in the catalogs, each
    /// with its state (the Plugins panel's list).
    /// </summary>
    static (List<Fields> Rows, List<string> Problems) CatalogRows(Catalog c, bool refresh)
    {
        var (entries, problems) = CatalogIndex.Load(Catalogs(c.Cfg), refresh);
        var rows = new List<Fields>();
        var lang = Locale.Languages.Detect(c.Cfg.Language);
        foreach (var p in c.All)
        {
            var e = LockedEntry(c.Lock, p);
            // Its catalog entry only if it's from there: a catalog doesn't
            // get to name the author of a built-in, or of another repo's plugin.
            var listed = e == null ? null : entries.FirstOrDefault(x => x.Id == p.Id && SameSource(x.Source, e.Source));
            var origin = c.Origin(p);
            Locale.Catalog? cat = null;
            try
            {
                cat = Locale.Catalog.Load(p.Id, p.Dir, lang);
            }
            catch (MyArchException) { } // its page says what's wrong
            // As on its page: its catalog entry's words, if it's from there.
            var tr = listed?.TranslatedTo(lang) ?? (Name: "", Description: "");
            rows.Add(new Fields
            {
                { "id", p.Id },
                { "name", cat?.TryT("plugin.name") ?? NonEmpty(tr.Name) ?? p.Meta.Name },
                { "description", cat?.TryT("plugin.description") ?? NonEmpty(tr.Description) ?? p.Meta.Description },
                { "author", listed?.Author ?? (origin == "built-in" ? "myarch" : "") },
                { "version", p.Meta.Version },
                { "categories", p.CategoriesOrGuess() },
                { "state", origin.StartsWith("git ") ? "installed" : origin == "local" ? "local" : "built-in" },
                { "enabled", c.Cfg.IsOn(p) },
                { "applies", Applies(p) },
                { "for_this_machine", p.Hardware != null && p.Hardware.Matches(Hardware.ThisMachine.Get()).Ok },
                { "source", e?.Source ?? "" },
                { "homepage", listed?.Homepage ?? "" },
            });
        }
        foreach (var (id, err) in c.Broken)
            rows.Add(new Fields { { "id", id }, { "name", id }, { "description", err }, { "state", "broken" }, { "enabled", false } });
        foreach (var e in entries.Where(x => c.All.All(p => p.Id != x.Id) && !c.Broken.ContainsKey(x.Id)))
            rows.Add(new Fields
            {
                { "id", e.Id },
                { "name", e.In(lang).Name },
                { "description", e.In(lang).Description },
                { "author", e.Author },
                { "version", "" },
                { "categories", e.Categories },
                { "state", "available" },
                { "enabled", false },
                { "applies", true },
                { "for_this_machine", false },
                { "source", e.Source + (e.Ref != "" ? "#" + e.Ref : "") },
                { "homepage", e.Homepage },
            });
        return (rows, problems);
    }

    static void PluginsCatalog(bool json, bool refresh, string[] terms)
    {
        var c = Catalog.Load();
        var (rows, problems) = CatalogRows(c, refresh);
        string Field(Fields f, string k) => f.FirstOrDefault(kv => kv.Key == k).Value?.ToString() ?? "";
        if (terms.Length > 0)
            rows = rows.Where(r => terms.All(t =>
                (Field(r, "id") + " " + Field(r, "name") + " " + Field(r, "description") + " " + string.Join(" ",
                    (r.FirstOrDefault(kv => kv.Key == "categories").Value as IEnumerable<string>) ?? []))
                .Contains(t, StringComparison.OrdinalIgnoreCase))).ToList();
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields { { "plugins", rows }, { "problems", problems } }));
            return;
        }
        foreach (var r in rows)
            Console.WriteLine($"{Field(r, "id"),-24} {Field(r, "state"),-10} {Field(r, "description")}");
        foreach (var pr in problems) Console.Error.WriteLine($"warning: {pr}");
    }

    /// <summary>A catalog id's source (url#ref), or the argument as it is.</summary>
    static (string Arg, string? CatalogId) FromCatalog(Catalog c, string arg)
    {
        if (!Plugin.IdPattern().IsMatch(arg) || Directory.Exists(arg)) return (arg, null);
        var e = CatalogIndex.Load(Catalogs(c.Cfg), false).Entries.FirstOrDefault(x => x.Id == arg)
            ?? throw new MyArchException($"no plugin \"{arg}\" in the catalogs (myarch plugins search)");
        return (e.Source + (e.Ref != "" ? "#" + e.Ref : ""), e.Id);
    }

    /// <summary>preview: a plugin fetched to look at, not installed: its page as the panel shows it.</summary>
    static void PluginsPreview(string arg, bool json)
    {
        var c = Catalog.Load();
        var (src, catalogId) = FromCatalog(c, arg);
        var (source, @ref) = Git.ParseSource(src);
        using var st = Git.Clone(source, @ref, "");
        if (catalogId != null && st.Plugin.Id != catalogId)
            throw new MyArchException($"the catalog's {catalogId} is plugin \"{st.Plugin.Id}\" at {source}");
        var f = Detail(c, st.Plugin, new Entry { Id = st.Plugin.Id, Source = source, Ref = @ref, Commit = st.Commit }, st.Commit);
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(f));
            return;
        }
        Console.WriteLine($"{Style.Bold}{st.Plugin.Meta.Name}{Style.Reset} {st.Plugin.Meta.Version} ({st.Plugin.Id}) at {st.Commit[..10]}");
        Console.WriteLine(st.Plugin.Meta.Description);
        Console.WriteLine("\nIt would be able to:");
        foreach (var cap in st.Plugin.Capabilities()) Console.WriteLine($"  - {cap}");
    }

    /// <summary>A TOML value as JSON.</summary>
    static object? JsonValue(object v) => v switch
    {
        Tomlyn.Model.TomlArray a => a.Select(x => JsonValue(x!)).ToList(),
        Tomlyn.Model.TomlTable t => new SortedDictionary<string, object?>(t.ToDictionary(kv => kv.Key, kv => JsonValue(kv.Value!)), StringComparer.Ordinal),
        Tomlyn.Model.TomlTableArray ta => ta.Select(x => JsonValue(x)).ToList(),
        Tomlyn.TomlDateTime d => d.ToString(),
        _ => v,
    };

    /// <summary>A setting's value as Go's %#v wrote it.</summary>
    static string GoValue(object v) => v switch
    {
        string s => GoFormat.Quote(s),
        bool b => b ? "true" : "false",
        long n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double f => GoFormat.Float(f) is var s2 && !s2.Contains('.') && !s2.Contains('e') ? s2 : GoFormat.Float(f),
        Tomlyn.Model.TomlArray a => "[]interface {}{" + string.Join(", ", a.Select(x => GoValue(x!))) + "}",
        _ => v.ToString() ?? "",
    };

    /// <summary>ProblemsOf keeps the problems of the given plugins.</summary>
    static List<string> ProblemsOf(List<Problem> problems, params string[] ids) =>
        problems.Where(p => ids.Contains(p.Plugin)).Select(p => p.Text).ToList();

    static void PluginsEnable(List<string> ids)
    {
        var c = Catalog.Load();
        foreach (var id in ids)
        {
            if (c.Broken.TryGetValue(id, out var err)) throw new MyArchException(err);
            if (c.Find(id) == null) throw new MyArchException($"no plugin \"{id}\"");
        }
        foreach (var id in ids) c.Cfg.TurnOn(c.Find(id)!);
        var pr = ProblemsOf(Dependencies.Unmet(c.Enabled(), c.All, c.Broken), [.. ids]);
        if (pr.Count > 0) throw new MyArchException("nothing enabled:\n  " + string.Join("\n  ", pr));
        c.Cfg.Save();
        Console.WriteLine($"enabled {string.Join(", ", ids)}; apply it with: myarch apply");
    }

    static void PluginsDisable(List<string> ids)
    {
        var c = Catalog.Load();
        foreach (var id in ids)
            if (c.Find(id) == null && !c.Broken.ContainsKey(id) && !c.Cfg.IsDisabled(id) && !c.Cfg.Enabled.Contains(id))
                throw new MyArchException($"no plugin \"{id}\"");
        var rest = c.Enabled(ids);
        var problems = new List<string>();
        foreach (var id in ids)
        {
            var d = Dependencies.Dependents(id, rest);
            if (d.Count > 0) problems.Add($"{id} is needed by {string.Join(", ", d)}");
        }
        if (problems.Count > 0)
        {
            // Everything that would have to go too, however indirectly.
            var all = new List<string>(ids);
            for (var i = 0; i < all.Count; i++)
                foreach (var d in Dependencies.Dependents(all[i], c.Enabled(all)))
                    if (!all.Contains(d)) all.Add(d);
            throw new MyArchException($"nothing disabled:\n  {string.Join("\n  ", problems)}\n  to disable them all: myarch plugins disable {string.Join(" ", all)}");
        }
        foreach (var id in ids)
            if (c.Find(id) is { } p) c.Cfg.TurnOff(p);
            else if (!c.Cfg.IsDisabled(id)) c.Cfg.Disabled.Add(id);
        c.Cfg.Save();
        Console.WriteLine($"disabled {string.Join(", ", ids)}; myarch apply takes away what it generated");
    }

    /// <summary>Approve shows what a plugin could do and asks. Without a terminal to ask on, only -y says yes.</summary>
    static bool Approve(string question, List<string> caps, bool yes)
    {
        foreach (var cap in caps) Console.WriteLine($"  - {cap}");
        if (yes) return true;
        if (!IsTerminal(0)) throw new MyArchException("no terminal to ask on; read the list above and approve it with -y");
        return Confirm(question);
    }

    /// <summary>
    /// add: a plugin from git or a catalog, once its capabilities are approved.
    /// commit, when given, is the one the person was shown (the Plugins
    /// panel's page): anything else is refused, not approved unseen.
    /// </summary>
    static void PluginsAdd(string arg, bool yes, string commit = "")
    {
        var c = Catalog.Load();
        var (src, catalogId) = FromCatalog(c, arg);
        var (source, @ref) = Git.ParseSource(src);
        if (Directory.Exists(source)) source = Path.GetFullPath(source);
        Console.WriteLine($"fetching {source}…");
        using var st = Git.Clone(source, @ref, "");
        var p = st.Plugin;
        var id = p.Id;
        if (catalogId != null && id != catalogId)
            throw new MyArchException($"the catalog's {catalogId} is plugin \"{id}\" at {source}: not installed");
        if (commit != "" && st.Commit != commit)
            throw new MyArchException($"{id} is at {st.Commit[..10]} now, not {(commit.Length > 10 ? commit[..10] : commit)} as shown: not installed; look at it again");
        var old = c.Find(id);
        if (old != null && c.Origin(old) == "built-in")
            throw new MyArchException($"it's called \"{id}\", like a built-in plugin; it can't replace it");
        if (old != null)
            throw new MyArchException($"{id} is already installed ({c.Origin(old)}); update it with: myarch plugins update {id}");
        if (c.Broken.TryGetValue(id, out var err))
            throw new MyArchException($"a plugin called {id} is already there, and doesn't load: {err}");
        if (c.Lock.Get(id) != null)
            throw new MyArchException($"{id} is in plugins.lock already; install it with: myarch plugins sync");
        var pr = ProblemsOf(Dependencies.Unmet([.. c.Enabled(), p], [.. c.All, p], c.Broken), id);
        if (pr.Count > 0) throw new MyArchException("not installed:\n  " + string.Join("\n  ", pr));
        Console.WriteLine($"\n{Style.Bold}{p.Meta.Name}{Style.Reset} {p.Meta.Version} ({id}) at {st.Commit[..10]}");
        if (p.Meta.Description != "") Console.WriteLine(p.Meta.Description);
        Console.WriteLine("\nIt will be able to:");
        var caps = p.Capabilities();
        if (!Approve("Install it, and allow all this?", caps, yes))
        {
            Console.WriteLine("nothing installed");
            return;
        }
        // The lock first: a checkout in the plugin folder without its entry
        // would be refused (or worse, taken for your own).
        c.Lock.Put(new Entry { Id = id, Source = source, Ref = @ref, Commit = st.Commit, Approved = caps, Catalog = catalogId != null });
        c.Lock.Save();
        try
        {
            st.Accept();
        }
        catch (Exception)
        {
            c.Lock.Delete(id);
            try
            {
                c.Lock.Save();
            }
            catch (Exception) { } // the error that matters is Accept's
            throw;
        }
        Console.WriteLine($"installed {id}; apply it with: myarch apply");
    }

    static void PluginsUpdate(List<string> args, bool yes)
    {
        var c = Catalog.Load();
        var refs = new Dictionary<string, string>();
        var ids = new List<string>();
        foreach (var a in args)
        {
            var i = a.IndexOf('#');
            var id = i < 0 ? a : a[..i];
            if (i >= 0)
            {
                var r = a[(i + 1)..];
                Git.CheckRef(r);
                refs[id] = r;
            }
            ids.Add(id);
        }
        if (ids.Count == 0)
        {
            ids.AddRange(c.Lock.Plugins.Select(e => e.Id));
            if (ids.Count == 0)
            {
                Console.WriteLine("no plugins from git (built-in ones update with myarch)");
                return;
            }
        }
        // Installed from a catalog that pins a version (ref = "v1.0"): its
        // newer pin is the update. A ref given here wins.
        var (listed, _) = CatalogIndex.Load(Catalogs(c.Cfg), false);
        var following = new HashSet<string>();
        foreach (var id in ids)
            if (!refs.ContainsKey(id) && c.Lock.Get(id) is { Catalog: true } le
                && listed.FirstOrDefault(x => x.Id == id) is { Ref: not "" } ce && SameSource(ce.Source, le.Source) && ce.Ref != le.Ref)
            {
                refs[id] = ce.Ref;
                following.Add(id);
            }
        // What others require first, so a newer version they need is there
        // when they're looked at.
        ids = DependenciesFirst(ids, c);
        var failed = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                UpdateOne(c, id, refs.TryGetValue(id, out var r) ? r : null, yes, following.Contains(id));
            }
            catch (Exception e) when (e is MyArchException or IOException or UnauthorizedAccessException)
            {
                // One plugin's trouble doesn't stop the others.
                failed.Add(e is MyArchException ? e.Message : $"{id}: {e.Message}");
            }
        }
        if (failed.Count > 0) throw new MyArchException(string.Join("\n", failed));
    }

    static string? NonEmpty(string s) => s != "" ? s : null;

    /// <summary>Two ways of writing one repository: …/x, …/x/, …/x.git.</summary>
    internal static bool SameSource(string a, string b)
    {
        static string N(string s)
        {
            s = s.TrimEnd('/');
            return s.EndsWith(".git") ? s[..^4] : s;
        }
        return N(a) == N(b);
    }

    static List<string> DependenciesFirst(List<string> ids, Catalog c)
    {
        var out_ = new List<string>();
        var done = new HashSet<string>();
        void Visit(string id, int depth)
        {
            if (done.Contains(id) || depth > ids.Count) return;
            if (c.Find(id) is { } p)
                foreach (var s in p.Meta.Requires)
                {
                    Requirement r;
                    try
                    {
                        r = Requirement.Parse(s);
                    }
                    catch (MyArchException) { continue; }
                    if (ids.Contains(r.Id)) Visit(r.Id, depth + 1);
                }
            if (done.Add(id)) out_.Add(id);
        }
        foreach (var id in ids) Visit(id, 0);
        return out_;
    }

    static HashSet<string> ProblemKeys(List<Problem> prs) => prs.Select(p => p.Plugin + "\0" + p.Requires).ToHashSet();

    /// <summary>
    /// UpdateOne moves one plugin from git to its ref's newest commit, or to
    /// newRef. followingCatalog: newRef is its catalog's (not the person's
    /// pick): it stays the catalog's, and never goes back a version.
    /// </summary>
    static void UpdateOne(Catalog c, string id, string? newRef, bool yes, bool followingCatalog = false)
    {
        var e = c.Lock.Get(id);
        if (e == null)
        {
            if (c.Find(id) is { } p0) throw new MyArchException($"{id} is {c.Origin(p0)}, not from git");
            throw new MyArchException($"no plugin \"{id}\" from git");
        }
        var next = e.Copy();
        if (newRef != null) next.Ref = newRef;
        // A ref picked by hand: the catalog no longer decides.
        if (newRef != null && !followingCatalog) next.Catalog = false;
        void Save()
        {
            var old = e.Copy();
            c.Lock.Put(next);
            try
            {
                c.Lock.Save();
            }
            catch (Exception)
            {
                c.Lock.Put(old);
                throw;
            }
        }
        var before = ProblemKeys(Dependencies.Unmet(c.Enabled(), c.All, c.Broken));
        using var m = Git.Update(e, next.Ref);
        if (m == null)
        {
            if (next.Ref != e.Ref || next.Catalog != e.Catalog)
            {
                Save();
                Console.WriteLine($"{id}: up to date, now following {next.Ref}");
                return;
            }
            Console.WriteLine($"{id}: up to date");
            return;
        }
        if (followingCatalog && c.Find(id) is { } cur && Versions.Compare(m.Plugin.Meta.Version, cur.Meta.Version) < 0)
            throw new MyArchException($"{id}: the catalog's {next.Ref} is {m.Plugin.Meta.Version}, older than {cur.Meta.Version}: not updated (myarch plugins update {id}#{next.Ref} to go there anyway)");
        // The plugins as they would be: does everything still fit together?
        var all = c.All.Select(p => p.Id == id ? m.Plugin : p).ToList();
        var after = new Catalog(c.Cfg, c.Lock, all, c.Broken);
        var broken = Dependencies.Unmet(after.Enabled(), all, c.Broken)
            .Where(pr => !before.Contains(pr.Plugin + "\0" + pr.Requires)).Select(pr => pr.Text).ToList();
        if (broken.Count > 0) throw new MyArchException($"{id} not updated:\n  {string.Join("\n  ", broken)}");
        Console.WriteLine($"\n{Style.Bold}{id}{Style.Reset} {m.From[..10]} → {m.To[..10]} ({m.Plugin.Meta.Version})");
        foreach (var l in m.Log.Split('\n'))
            if (l != "") Console.WriteLine("  " + l);
        var caps = m.Plugin.Capabilities();
        var extra = Plugin.NewCapabilities(e.Approved, caps);
        if (extra.Count > 0)
        {
            Console.WriteLine("\nThis version also wants to:");
            bool ok;
            try
            {
                ok = Approve("Update it, and allow this too?", extra, yes);
            }
            catch (MyArchException ex)
            {
                throw new MyArchException($"{id} not updated: {ex.Message}");
            }
            if (!ok) throw new MyArchException($"{id} not updated");
        }
        next.Commit = m.To;
        next.Approved = caps;
        try
        {
            m.Keep();
        }
        catch (MyArchException ex)
        {
            throw new MyArchException($"{id} not updated: {ex.Message}");
        }
        try
        {
            Save();
        }
        catch (Exception ex)
        {
            try
            {
                m.Back();
            }
            catch (MyArchException berr)
            {
                throw new MyArchException($"{id}: {ex.Message}; and it couldn't go back to {m.From[..10]}: {berr.Message} (run: myarch plugins sync)");
            }
            throw;
        }
        // Later updates see this version (a plugin that requires it).
        c.All = all;
        Console.WriteLine($"updated {id}; apply it with: myarch apply");
    }

    internal static void PluginsRemove(List<string> ids)
    {
        // A link plugins dev made in a built-in's place: only the link goes,
        // and the built-in (its settings with it) is back.
        var builtIn = Plugin.Discover([Paths.Join(Root(), "plugins")]).Plugins.Select(p => p.Id).ToHashSet();
        foreach (var id in ids.Where(id => builtIn.Contains(id) && IsLink(Paths.Join(Git.Dir, id))).ToList())
        {
            RemoveAll(Paths.Join(Git.Dir, id));
            Console.WriteLine($"unlinked {id}: the built-in {id} is back");
            ids.Remove(id);
        }
        if (ids.Count == 0) return;
        var c = Catalog.Load();
        foreach (var id in ids)
        {
            var p = c.Find(id);
            if (c.Lock.Get(id) != null) continue; // from git: removable, installed or not, loading or not
            // Linked by plugins dev: the link goes, the folder it points to stays.
            if (IsLink(Paths.Join(Git.Dir, id))) continue;
            if ((p != null && c.Origin(p) == "local") || (c.Broken.ContainsKey(id) && p == null))
                throw new MyArchException($"{id} is your own plugin, not from git: delete {Tilde(Paths.Join(Git.Dir, id))} yourself");
            if (p != null) throw new MyArchException($"{id} is built in; disable it instead: myarch plugins disable {id}");
            throw new MyArchException($"no plugin \"{id}\"");
        }
        var rest = c.Enabled(ids);
        foreach (var id in ids)
        {
            var d = Dependencies.Dependents(id, rest);
            if (d.Count > 0) throw new MyArchException($"nothing removed: {id} is needed by {string.Join(", ", d)}");
        }
        foreach (var id in ids) c.Lock.Delete(id);
        // The lock first: a folder left behind without its entry is refused.
        c.Lock.Save();
        foreach (var id in ids)
        {
            RemoveAll(Paths.Join(Git.Dir, id));
            if (c.Cfg.Plugins.Remove(id)) Console.WriteLine($"dropped [plugins.{id}] from config.toml");
            c.Cfg.Disabled.RemoveAll(d => d == id);
            c.Cfg.Enabled.RemoveAll(d => d == id);
        }
        c.Cfg.Save();
        Console.WriteLine($"removed {string.Join(", ", ids)}; myarch apply takes away what it generated");
    }

    /// <summary>RemoveAll is Go's os.RemoveAll: a folder with what's in it, a file, or a link (not what it points to).</summary>
    static void RemoveAll(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (fi.LinkTarget != null || fi.Exists) fi.Delete();
            else if (Directory.Exists(path)) Directory.Delete(path, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MyArchException($"removing {Tilde(path)}: {e.Message}");
        }
    }

    /// <summary>
    /// PluginsSync makes the plugin folder match plugins.lock: each plugin at
    /// its commit, needing nothing beyond what was approved. It never asks: the
    /// approvals are in the lock.
    /// </summary>
    static void PluginsSync()
    {
        var c = Catalog.Load();
        var failed = new List<string>();
        foreach (var e in c.Lock.Plugins)
        {
            try
            {
                Console.WriteLine($"{e.Id,-18} {SyncOne(e)}");
            }
            catch (Exception ex) when (ex is MyArchException or IOException or UnauthorizedAccessException)
            {
                failed.Add(ex is MyArchException ? ex.Message : $"{e.Id}: {ex.Message}");
            }
        }
        // Checkouts the lock doesn't list (dropped on another machine) are
        // refused by apply; say so here.
        string[] dirs = [];
        try
        {
            if (Directory.Exists(Git.Dir)) dirs = Directory.GetDirectories(Git.Dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // as Go's ReadDir error: nothing to list
        foreach (var dir in dirs.Order(StringComparer.Ordinal))
            {
                var name = Paths.Base(dir);
                if (c.Lock.Get(name) == null && IsCheckout(dir) && !IsLink(dir))
                    failed.Add($"{name} isn't in plugins.lock: delete {Tilde(dir)}, or add it again");
            }
        if (c.Lock.Plugins.Count == 0 && failed.Count == 0) Console.WriteLine("plugins.lock lists no plugins");
        if (failed.Count > 0) throw new MyArchException(string.Join("\n", failed));
    }

    static string SyncOne(Entry e)
    {
        var dir = Paths.Join(Git.Dir, e.Id);
        void Check(Plugin p)
        {
            if (p.Id != e.Id) throw new MyArchException($"{e.Id}: the commit in plugins.lock is plugin \"{p.Id}\"");
            var extra = Plugin.NewCapabilities(e.Approved, p.Capabilities());
            if (extra.Count > 0)
                throw new MyArchException($"{e.Id} needs what plugins.lock doesn't approve:\n    {string.Join("\n    ", extra)}");
        }
        if (!Paths.Exists(dir))
        {
            Staged st;
            try
            {
                st = Git.Clone(e.Source, "", e.Commit);
            }
            catch (MyArchException ex)
            {
                throw new MyArchException($"{e.Id}: {ex.Message}");
            }
            using (st)
            {
                Check(st.Plugin);
                st.Accept();
            }
            return "installed";
        }
        string? head = null;
        try
        {
            head = Git.Head(dir);
        }
        catch (MyArchException) { }
        if (head == e.Commit)
        {
            Git.Verify(dir, e, Plugin.Load(dir));
            return "ok";
        }
        Plugin moved;
        try
        {
            moved = Git.Goto(e);
        }
        catch (MyArchException ex)
        {
            throw new MyArchException($"{e.Id}: {ex.Message}");
        }
        Check(moved);
        return "moved to " + e.Commit[..10];
    }
}
