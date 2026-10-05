using System.Globalization;
using Mazapan.Locale;
using Mazapan.Plugins;
using Mazapan.Themes;
using Mazapan.Util;
using Scriban;
using Scriban.Runtime;

namespace Mazapan.Rendering;

/// <summary>Action is one plugin action, rendered: what the command palette lists.</summary>
public sealed record RenderedAction(string Plugin, string Name, string Run, string Key, bool Terminal, string Keywords, long Home = 0, string Glyph = "", string Label = "", bool Confirm = false, string KeySetting = "", bool Setting = false);

/// <summary>Check is one plugin check, rendered.</summary>
public sealed record RenderedCheck(string Plugin, string Name, string Run, long Timeout, bool Session, bool Before = false);

/// <summary>File is one rendered target.</summary>
public sealed class RenderedFile
{
    public string Plugin = "";
    public string Path = ""; // absolute
    public string Content = "";
    public string Reload = ""; // rendered reload command, may be empty
    /// <summary>Merge: a file shared with its app or person; only these keys are mazapan's (see apply).</summary>
    public string Merge = "";
    /// <summary>Busy: its app is running (the target's busy marker exists): left for the next apply.</summary>
    public bool Busy;
    /// <summary>System: written as root (sudo), by apply --system only.</summary>
    public bool System;
    /// <summary>Reboot: it takes effect after a reboot.</summary>
    public bool Reboot;
}

/// <summary>Output is everything the plugins produce for one theme and language.</summary>
public sealed class Output
{
    public List<RenderedFile> Files { get; } = [];
    public List<RenderedCheck> Checks { get; } = [];
    public List<RenderedAction> Actions { get; } = [];
}

/// <summary>
/// Scriban's own functions, as templates get them: frozen (shared by every
/// plugin, so none can redefine string.upcase for the next), and without
/// what reads or runs something else (include, object.eval), what changes on
/// every apply (date.now, math.random) and what Native AOT doesn't have.
/// </summary>
static class Builtins
{
    public static readonly ScriptObject Shared = Make();

    static ScriptObject Make()
    {
        var b = TemplateContext.GetDefaultBuiltinObject();
        b.Remove("include");
        b.Remove("include_join");
        void Drop(string obj, params string[] names)
        {
            if (b[obj] is ScriptObject o)
                foreach (var n in names) o.Remove(n);
        }
        Drop("object", "eval", "eval_template", "from_json", "to_json", "has_key", "has_value");
        Drop("date", "now", "utc_now", "parse");
        Drop("math", "random", "uuid");
        Drop("string", "index_of", "slice");
        Drop("timespan", "zero");
        foreach (var k in b.Keys)
            if (b[k] is ScriptObject o) o.IsReadOnly = true;
        b.IsReadOnly = true;
        return b;
    }
}

/// <summary>Turns plugin templates plus a theme into file contents.</summary>
public static class Renderer
{
    /// <summary>
    /// How long one plugin's templates may take: a plugin from git whose
    /// template loops forever must not hang mazapan.
    /// </summary>
    public static TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Places are where a target goes: once at its output, or, with each,
    /// into every directory holding a file its patterns match (every Firefox
    /// profile has a prefs.js). A place that appears later is taken on the
    /// next apply; one that's gone takes its file with it (the dir is gone).
    /// </summary>
    public static List<string> Places(Target tg)
    {
        if (tg.Each.Count == 0) return [""];
        var seen = new HashSet<string>();
        var out_ = new List<string>();
        foreach (var pattern in tg.Each)
            foreach (var m in Glob.Expand(Paths.ExpandHome(pattern)))
            {
                var d = Paths.Dir(m);
                // ~/.mozilla/firefox linked to ~/.config/mozilla/firefox: one
                // profile, one copy.
                var real = Paths.Real(d) ?? d;
                if (seen.Add(real)) out_.Add(d);
            }
        out_.Sort(StringComparer.Ordinal);
        return out_;
    }

    /// <summary>Landing resolves the symlinks of path's nearest existing folder, to see where a write would land.</summary>
    static string Landing(string path)
    {
        var dir = Paths.Dir(path);
        var rest = Paths.Base(path);
        while (true)
        {
            if (Paths.Real(dir) is { } r) return Paths.Join(r, rest);
            var parent = Paths.Dir(dir);
            if (parent == dir) return path;
            rest = Paths.Join(Paths.Base(dir), rest);
            dir = parent;
        }
    }

    /// <summary>
    /// All renders every target of every plugin. Output paths are collected
    /// first so templates can see each other's outputs through the "under"
    /// function. overrides holds per-plugin settings from config.toml, keyed
    /// by plugin id; lang is the language to render text in (see
    /// Languages.Detect).
    /// </summary>
    public static Output All(IReadOnlyList<Plugin> plugins, Theme t,
        Func<string, IReadOnlyDictionary<string, object>?> overrides, string lang)
    {
        var outputs = new List<string>();
        var owner = new Dictionary<string, string>();
        foreach (var p in plugins)
            foreach (var tg in p.Targets)
                foreach (var d in Places(tg))
                {
                    var o = d == "" ? Paths.ExpandHome(tg.Output) : Paths.Join(d, tg.Output);
                    if (Plugin.Reserved(o) || Plugin.Reserved(Landing(o)))
                        throw new MazapanException($"plugin {p.Id}: {o} is mazapan's own; no plugin writes there");
                    if (owner.TryGetValue(o, out var other))
                        throw new MazapanException($"{o}: both {other} and {p.Id} generate it");
                    owner[o] = p.Id;
                    outputs.Add(o);
                }
        outputs.Sort(StringComparer.Ordinal);

        var home = Paths.Home;
        var output = new Output();
        var code = lang.Split('_')[0];

        // First every plugin's checks and actions, so that templates can see
        // all the actions (the command palette lists them).
        var ready = new List<(Plugin p, Scope scope)>();
        foreach (var p in plugins)
        {
            if (p.Targets.Count == 0 && p.Checks.Count == 0 && p.Actions.Count == 0) continue;
            var cat = Catalog.Load(p.Id, p.Dir, lang);
            var settings = p.Resolve(overrides(p.Id));
            var scope = new Scope(p, t, settings, outputs, cat, new()
            {
                ["plugin"] = p.Id,
                ["home"] = home,
                // Whose desktop this is (the account applying it): for the
                // login screen's autologin (through `username`).
                ["user"] = Environment.GetEnvironmentVariable("USER") is { Length: > 0 } u ? u : Environment.UserName,
                ["lang"] = lang,
                ["lang_code"] = code,
            });
            foreach (var c in p.Checks)
            {
                var name = scope.String(c.Name, "check name");
                var run = scope.String(c.Run, $"check \"{name}\"");
                output.Checks.Add(new(p.Id, name, run, c.Timeout <= 0 ? 15 : c.Timeout, c.Session, c.Before));
            }
            for (var n = 0; n < p.Actions.Count; n++)
            {
                var a = p.Actions[n];
                var f = new[] { a.Name, a.Run, a.Key, a.Keywords, a.Label }.Select(s => scope.String(s, $"actions[{n}]")).ToArray();
                // A key set to "" in config.toml unbinds it: an action with
                // neither a command nor a key left has nothing to offer.
                if (f[1].Trim() == "" && f[2].Trim() == "") continue;
                output.Actions.Add(new(p.Id, f[0], f[1], f[2], a.Terminal, f[3], a.Home, a.Glyph, f[4], a.Confirm, a.KeySetting, a.Setting));
            }
            ready.Add((p, scope));
        }

        foreach (var (p, scope) in ready)
        {
            scope.Actions = output.Actions;
            foreach (var tg in p.Targets)
                foreach (var place in Places(tg))
                {
                    scope.Set("place", place);
                    var path = place == "" ? Paths.ExpandHome(tg.Output) : Paths.Join(place, tg.Output);
                    var content = scope.File(tg.Template);
                    var reload = scope.String(tg.Reload, "reload");
                    var busy = tg.Busy != "" && place != "" && Paths.Exists(Paths.Join(place, tg.Busy));
                    output.Files.Add(new RenderedFile
                    {
                        Plugin = p.Id,
                        Path = path,
                        Content = content,
                        Reload = reload,
                        Merge = tg.Merge,
                        Busy = busy,
                        System = tg.System,
                        Reboot = tg.Reboot,
                    });
                }
        }
        return output;
    }

    /// <summary>
    /// Scope is one plugin's templates, as they render: the data every
    /// template sees, the functions, and the plugin's shared functions
    /// (_*.tmpl). Nothing a template does can change what the next one sees:
    /// every name is read-only.
    /// </summary>
    sealed class Scope
    {
        readonly Plugin plugin;
        readonly TemplateContext ctx;
        readonly ScriptObject globals;
        readonly Dictionary<string, Template> files = [];

        readonly Theme theme;
        readonly Dictionary<string, object> settings;

        /// <summary>Every plugin's actions, once they're all rendered.</summary>
        public List<RenderedAction> Actions = [];

        public Scope(Plugin p, Theme t, Dictionary<string, object> settings, List<string> outputs, Catalog cat, ScriptObject data)
        {
            plugin = p;
            theme = t;
            this.settings = settings;
            globals = data;
            Functions.Add(globals, t, outputs, cat);
            globals.Add("place", "");
            globals.Add("theme", new ScriptObject());
            globals.Add("settings", new ScriptObject());
            globals.Add("actions", new ScriptArray());
            globals.Add("machine", new ScriptObject());
            ctx = new TemplateContext(Builtins.Shared)
            {
                StrictVariables = true,
                EnableRelaxedMemberAccess = false,
                EnableRelaxedFunctionAccess = false,
                EnableRelaxedIndexerAccess = false,
                EnableRelaxedTargetAccess = false,
                AutoIndent = false,
                LoopLimit = 100_000,
                RecursiveLimit = 100,
                MemberRenamer = m => m.Name,
            };
            ctx.PushCulture(CultureInfo.InvariantCulture);
            ctx.PushGlobal(globals);
            var lib = Library.Load(p.Dir);
            foreach (var name in lib.Functions.Keys)
                if (globals.ContainsKey(name) || Builtins.Shared.ContainsKey(name))
                    throw new MazapanException($"plugin {p.Id}: a shared function can't be called {name}: that name is taken");
            foreach (var l in lib.Templates) Run(l, "shared functions", isolate: false);
            foreach (var name in globals.Keys.ToList()) globals.SetReadOnly(name, true);
        }

        public void Set(string name, object value)
        {
            globals.SetReadOnly(name, false);
            globals[name] = value;
            globals.SetReadOnly(name, true);
        }

        /// <summary>
        /// Renders tmpl. What it assigns lives in a scope of its own
        /// (isolate), gone when it's done; shared functions are defined for
        /// good.
        /// </summary>
        string Run(Template tmpl, string what, bool isolate = true)
        {
            // Names it makes (its variables, a for's item) go when it's done;
            // ours are read-only, so it can't change them.
            // The data, made anew for each: what a template changes in it (an
            // array's item: Scriban doesn't stop that) stays in that template.
            Set("theme", Model.Theme(theme));
            Set("settings", Model.Settings(settings));
            Set("actions", Model.Actions(Actions));
            Set("machine", Model.Machine(Hardware.ThisMachine.Get()));
            var before = isolate ? globals.Keys.ToHashSet() : null;
            using var deadline = new CancellationTokenSource(Timeout);
            ctx.CancellationToken = deadline.Token;
            try
            {
                return tmpl.Render(ctx);
            }
            catch (Scriban.Syntax.ScriptRuntimeException) when (deadline.IsCancellationRequested)
            {
                throw new MazapanException($"plugin {plugin.Id}: {what}: took longer than {Timeout.TotalSeconds:0} s");
            }
            catch (Scriban.Syntax.ScriptRuntimeException e)
            {
                throw new MazapanException($"plugin {plugin.Id}: {what}: {e.Message}");
            }
            catch (OperationCanceledException)
            {
                throw new MazapanException($"plugin {plugin.Id}: {what}: took longer than {Timeout.TotalSeconds:0} s");
            }
            finally
            {
                if (before != null)
                    foreach (var k in globals.Keys.Where(k => !before.Contains(k)).ToList()) globals.Remove(k);
            }
        }

        /// <summary>A template file of the plugin.</summary>
        public string File(string name)
        {
            if (!files.TryGetValue(name, out var tmpl))
            {
                var path = Paths.Join(plugin.Dir, name);
                if (Paths.Dir(path) != Paths.Clean(plugin.Dir))
                    throw new MazapanException($"plugin {plugin.Id}: template {name} is outside the plugin");
                try
                {
                    tmpl = Tmpl.Parse(System.IO.File.ReadAllText(path), path);
                }
                catch (MazapanException e)
                {
                    throw new MazapanException($"plugin {plugin.Id}: {e.Message}");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    throw new MazapanException($"plugin {plugin.Id}: {name}: {e.Message}");
                }
                files[name] = tmpl;
            }
            return Run(tmpl, name);
        }

        /// <summary>A string from the manifest (a name, a command, a key).</summary>
        public string String(string s, string what)
        {
            if (s == "") return "";
            Template tmpl;
            try
            {
                tmpl = Tmpl.Parse(s, what);
            }
            catch (MazapanException e)
            {
                throw new MazapanException($"plugin {plugin.Id}: {what}: {e.Message}");
            }
            return Run(tmpl, what);
        }
    }
}
