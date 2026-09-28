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
            if (cfg.IsDisabled(p.Id)) continue;
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

    /// <summary>Trusted: built-ins and your own plugins are; one from git must be what plugins.lock says.</summary>
    static void Trusted(PluginsLock lck, Plugin p)
    {
        var e = lck.Get(p.Id);
        var fromGit = Paths.Dir(p.Dir) == Git.Dir;
        if (e != null && !fromGit)
            throw new MyArchException($"{p.Id} is in plugins.lock but not installed (run: myarch plugins sync)");
        if (e == null && fromGit && IsCheckout(p.Dir))
            throw new MyArchException($"{p.Id} is a git checkout that isn't in plugins.lock; add it with myarch plugins add, or delete {Tilde(p.Dir)}");
        if (e != null) Git.Verify(p.Dir, e, p);
    }

    static bool IsCheckout(string dir) => Paths.Exists(Paths.Join(dir, ".git"));

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
        var rest = new List<string>();
        foreach (var a in args)
        {
            if (a is "-y" or "--yes") yes = true;
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
                Need(1, 1, "ID");
                PluginsShow(rest[0]);
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
                Need(1, 1, "URL[#REF] [-y]");
                PluginsAdd(rest[0], yes);
                break;
            case "update":
                PluginsUpdate(rest, yes);
                break;
            case "remove":
                Need(1, -1, "ID...");
                PluginsRemove(rest);
                break;
            case "sync":
                Need(0, 0, "");
                PluginsSync();
                break;
            default:
                throw new MyArchException($"plugins {sub}: list, show, enable, disable, add, update, remove or sync");
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
            return All.Where(p => !cfg.IsDisabled(p.Id) && !s.Contains(p.Id)).ToList();
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
            var state = c.Cfg.IsDisabled(p.Id) ? "disabled" : "enabled";
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
        var state = c.Cfg.IsDisabled(id) ? "disabled" : "enabled";
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
        c.Cfg.Disabled = c.Cfg.Disabled.Where(d => !ids.Contains(d)).ToList();
        var pr = ProblemsOf(Dependencies.Unmet(c.Enabled(), c.All, c.Broken), [.. ids]);
        if (pr.Count > 0) throw new MyArchException("nothing enabled:\n  " + string.Join("\n  ", pr));
        c.Cfg.Save();
        Console.WriteLine($"enabled {string.Join(", ", ids)}; apply it with: myarch apply");
    }

    static void PluginsDisable(List<string> ids)
    {
        var c = Catalog.Load();
        foreach (var id in ids)
            if (c.Find(id) == null && !c.Broken.ContainsKey(id) && !c.Cfg.IsDisabled(id))
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
            if (!c.Cfg.IsDisabled(id)) c.Cfg.Disabled.Add(id);
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

    static void PluginsAdd(string arg, bool yes)
    {
        var c = Catalog.Load();
        var (source, @ref) = Git.ParseSource(arg);
        if (Directory.Exists(source)) source = Path.GetFullPath(source);
        Console.WriteLine($"fetching {source}…");
        using var st = Git.Clone(source, @ref, "");
        var p = st.Plugin;
        var id = p.Id;
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
        c.Lock.Put(new Entry { Id = id, Source = source, Ref = @ref, Commit = st.Commit, Approved = caps });
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
        // What others require first, so a newer version they need is there
        // when they're looked at.
        ids = DependenciesFirst(ids, c);
        var failed = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                UpdateOne(c, id, refs.TryGetValue(id, out var r) ? r : null, yes);
            }
            catch (Exception e) when (e is MyArchException or IOException or UnauthorizedAccessException)
            {
                // One plugin's trouble doesn't stop the others.
                failed.Add(e is MyArchException ? e.Message : $"{id}: {e.Message}");
            }
        }
        if (failed.Count > 0) throw new MyArchException(string.Join("\n", failed));
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

    static void UpdateOne(Catalog c, string id, string? newRef, bool yes)
    {
        var e = c.Lock.Get(id);
        if (e == null)
        {
            if (c.Find(id) is { } p0) throw new MyArchException($"{id} is {c.Origin(p0)}, not from git");
            throw new MyArchException($"no plugin \"{id}\" from git");
        }
        var next = e.Copy();
        if (newRef != null) next.Ref = newRef;
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
            if (next.Ref != e.Ref)
            {
                Save();
                Console.WriteLine($"{id}: up to date, now following {next.Ref}");
                return;
            }
            Console.WriteLine($"{id}: up to date");
            return;
        }
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

    static void PluginsRemove(List<string> ids)
    {
        var c = Catalog.Load();
        foreach (var id in ids)
        {
            var p = c.Find(id);
            if (c.Lock.Get(id) != null) continue; // from git: removable, installed or not, loading or not
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
                if (c.Lock.Get(name) == null && IsCheckout(dir))
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
