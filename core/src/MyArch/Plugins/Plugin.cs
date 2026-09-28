using System.Text.RegularExpressions;
using MyArch.Util;
using Tomlyn.Model;

namespace MyArch.Plugins;

/// <summary>
/// A plugin, read from its plugin.toml.
///
/// Built-in plugins use exactly this API; there is no private path for them.
/// </summary>
public sealed partial class Plugin
{
    /// <summary>APIVersion is the manifest API this core understands.</summary>
    public const int ApiVersion = 1;

    public string Dir { get; set; } = "";

    public sealed class MetaTable
    {
        public string Id = "", Name = "", Version = "", Description = "";
        public long Api;
        /// <summary>Requires: other plugins it needs, "id" or "id >= 1.2".</summary>
        public List<string> Requires = [];
    }

    public MetaTable Meta { get; } = new();

    /// <summary>Packages the plugin needs at runtime. Checked, not installed (yet).</summary>
    public List<string> Pacman { get; set; } = [];

    /// <summary>
    /// Settings are the plugin's knobs with their defaults. People override
    /// them in config.toml under [plugins.&lt;id&gt;]; templates read the
    /// result as settings.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = [];

    /// <summary>
    /// Targets are files the plugin generates. The core renders and writes
    /// them; plugins never touch the filesystem themselves.
    /// </summary>
    public List<Target> Targets { get; } = [];

    /// <summary>
    /// Checks say whether what the plugin is responsible for still works.
    /// `myarch doctor` runs them on demand; `myarch update` runs them after
    /// updating and rolls back when one fails.
    /// </summary>
    public List<Check> Checks { get; } = [];

    /// <summary>
    /// Actions are what the plugin lets you do: the command palette lists
    /// every plugin's, each with the command it runs and its keybinding.
    /// </summary>
    public List<Action> Actions { get; } = [];

    /// <summary>
    /// Coverage says which apps the plugin themes, so `myarch coverage` can
    /// tell which installed apps the theme doesn't reach.
    /// </summary>
    public sealed class CoverageTable
    {
        /// <summary>.desktop ids or executable names: "foot", "org.gnome.Nautilus".</summary>
        public List<string> Apps = [];
        /// <summary>Whole toolkits: terminal, gtk4, gtk3, qt6, qt5, electron, chromium, firefox, flatpak, web.</summary>
        public List<string> Toolkits = [];
    }

    public CoverageTable Coverage { get; } = new();

    public string Id => Meta.Id;

    static readonly HashSet<string> KnownToolkits =
        ["terminal", "gtk4", "gtk3", "qt6", "qt5", "electron", "chromium", "firefox", "flatpak", "web"];

    /// <summary>An id names the plugin's folder and its [plugins.&lt;id&gt;] table.</summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*\z")]
    public static partial Regex IdPattern();

    /// <summary>
    /// Resolve merges overrides from config.toml onto the plugin's defaults.
    /// Unknown keys and type mismatches are errors: a typo in config.toml must
    /// not be silently ignored.
    /// </summary>
    public Dictionary<string, object> Resolve(IReadOnlyDictionary<string, object>? overrides)
    {
        var out_ = new Dictionary<string, object>(Settings);
        if (overrides == null) return out_;
        foreach (var (k, v) in overrides)
        {
            if (!Settings.TryGetValue(k, out var def))
                throw new MyArchException($"[plugins.{Id}] {k}: unknown setting (known: {Keys(Settings)})");
            out_[k] = Coerce(def, v) ?? throw new MyArchException(
                $"[plugins.{Id}] {k} = {Show(v)}: want a {GoType(def)} like the default {Show(def)}");
        }
        return out_;
    }

    /// <summary>
    /// Coerce accepts v if it has the default's type. An integer is accepted
    /// where the default is a float (min_width = 480 for a 480.0 default).
    /// </summary>
    static object? Coerce(object def, object v) => def switch
    {
        double => v switch { double d => d, long n => (double)n, _ => null },
        long => v is long ? v : null,
        string => v is string ? v : null,
        bool => v is bool ? v : null,
        _ => def.GetType() == v.GetType() ? v : null,
    };

    static string GoType(object v) => v switch
    {
        double => "float64",
        long => "int64",
        string => "string",
        bool => "bool",
        TomlArray => "[]interface {}",
        _ => v.GetType().Name,
    };

    public static string Show(object v) => v switch
    {
        string s => s,
        bool b => b ? "true" : "false",
        double d => GoFormat.Float(d),
        long n => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
        TomlArray a => "[" + string.Join(" ", a.Select(x => Show(x!))) + "]",
        _ => v.ToString() ?? "",
    };

    static string Keys(Dictionary<string, object> m)
    {
        if (m.Count == 0) return "none";
        return "[" + string.Join(" ", m.Keys.Order(StringComparer.Ordinal)) + "]";
    }

    /// <summary>Load reads the plugin in dir.</summary>
    public static Plugin Load(string dir) => LoadManifest(Paths.Join(dir, "plugin.toml"));

    /// <summary>
    /// Discover loads every plugin under dirs; a plugin's folder is named after
    /// its id. An id found in an earlier directory shadows the same id in later
    /// ones (user dir before built-ins). A plugin that doesn't load is returned
    /// in broken, by id, and still shadows: a broken plugin from git must not
    /// quietly fall back to a built-in.
    /// </summary>
    public static (List<Plugin> Plugins, SortedDictionary<string, string> Broken) Discover(IEnumerable<string> dirs)
    {
        var byId = new Dictionary<string, Plugin>();
        var broken = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var d in dirs)
        {
            if (!Directory.Exists(d)) continue;
            var entries = Directory.GetFileSystemEntries(d).Select(Path.GetFileName).OfType<string>()
                .Order(StringComparer.Ordinal);
            foreach (var id in entries)
            {
                var path = Paths.Join(d, id, "plugin.toml");
                if (!File.Exists(path) || byId.ContainsKey(id) || broken.ContainsKey(id)) continue;
                try
                {
                    var p = LoadManifest(path);
                    if (p.Id != id)
                        throw new MyArchException($"{path}: id \"{p.Id}\", but its folder is \"{id}\": they must match");
                    byId[id] = p;
                }
                catch (MyArchException e)
                {
                    broken[id] = e.Message;
                }
            }
        }
        return (byId.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList(), broken);
    }

    /// <summary>
    /// Reserved says whether path is myarch's own: its config, plugins.lock,
    /// the plugins and themes it loads, what it remembers. No plugin writes
    /// there (bin/, for the scripts plugins ship, is the exception).
    /// </summary>
    public static bool Reserved(string path)
    {
        var home = Paths.Home;
        path = Paths.Clean(path);
        foreach (var r in new[] { ".config/myarch", ".local/state/myarch", ".local/share/myarch" })
        {
            var root = Paths.Join(home, r);
            if (path == root || path.StartsWith(root + "/"))
                return !path.StartsWith(Paths.Join(home, ".local/share/myarch/bin") + "/");
        }
        return false;
    }

    static Plugin LoadManifest(string path)
    {
        var table = Toml.ReadFile(path) ?? throw new MyArchException($"{path}: no such file");
        var p = new Plugin { Dir = Paths.Dir(path) };
        var r = new TomlReader(table, path);
        var meta = r.Sub("plugin");
        p.Meta.Id = meta.String("id");
        p.Meta.Name = meta.String("name");
        p.Meta.Version = meta.String("version");
        p.Meta.Api = meta.Int("api");
        p.Meta.Description = meta.String("description");
        p.Meta.Requires = meta.Strings("requires");
        p.Pacman = r.Sub("packages").Strings("pacman");
        if (r.Raw("settings") is { } settings)
            foreach (var (k, v) in settings) p.Settings[k] = v;
        foreach (var t in r.Array("targets"))
            p.Targets.Add(new Target
            {
                Template = t.String("template"),
                Output = t.String("output"),
                Reload = t.String("reload"),
                Merge = t.String("merge"),
                Each = t.Strings("each"),
                Busy = t.String("busy"),
            });
        foreach (var c in r.Array("checks"))
            p.Checks.Add(new Check
            {
                Name = c.String("name"),
                Run = c.String("run"),
                Timeout = c.Int("timeout"),
                Session = c.Bool("session"),
            });
        foreach (var a in r.Array("actions"))
            p.Actions.Add(new Action
            {
                Name = a.String("name"),
                Run = a.String("run"),
                Key = a.String("key"),
                Terminal = a.Bool("terminal"),
                Keywords = a.String("keywords"),
            });
        var cov = r.Sub("coverage");
        p.Coverage.Apps = cov.Strings("apps");
        p.Coverage.Toolkits = cov.Strings("toolkits");
        r.Done();
        p.Validate(path);
        return p;
    }

    void Validate(string path)
    {
        if (Meta.Id == "") throw new MyArchException($"{path}: [plugin] id is required");
        if (!IdPattern().IsMatch(Meta.Id))
            throw new MyArchException($"{path}: [plugin] id \"{Meta.Id}\": lowercase letters, digits and dashes");
        if (Meta.Api != ApiVersion)
            throw new MyArchException($"{path}: api = {Meta.Api}, this core supports api = {ApiVersion}");
        try
        {
            Versions.Parse(Meta.Version);
        }
        catch (MyArchException e)
        {
            throw new MyArchException($"{path}: [plugin] {e.Message}");
        }
        foreach (var req in Meta.Requires)
        {
            Requirement r;
            try
            {
                r = Requirement.Parse(req);
            }
            catch (MyArchException e)
            {
                throw new MyArchException($"{path}: [plugin] {e.Message}");
            }
            if (r.Id == Meta.Id) throw new MyArchException($"{path}: [plugin] requires itself");
        }
        for (var i = 0; i < Checks.Count; i++)
        {
            if (Checks[i].Name == "" || Checks[i].Run == "")
                throw new MyArchException($"{path}: checks[{i}] needs name and run");
            if (Checks[i].Timeout < 0 || Checks[i].Timeout > 3600)
                throw new MyArchException($"{path}: checks[{i}]: timeout is seconds, up to 3600");
        }
        foreach (var t in Coverage.Toolkits)
            if (!KnownToolkits.Contains(t)) throw new MyArchException($"{path}: coverage: unknown toolkit \"{t}\"");
        for (var i = 0; i < Actions.Count; i++)
            if (Actions[i].Name == "" || (Actions[i].Run == "" && Actions[i].Key == ""))
                throw new MyArchException($"{path}: actions[{i}] needs a name, and run or key");
        var home = Paths.Home;
        for (var i = 0; i < Targets.Count; i++)
        {
            var t = Targets[i];
            if (t.Template == "" || t.Output == "")
                throw new MyArchException($"{path}: targets[{i}] needs template and output");
            // A template is a file of the plugin: never a path out of its
            // folder (a plugin from git reading ~/.ssh into its output).
            if (t.Template.Contains('/') || t.Template.StartsWith('.') || !t.Template.EndsWith(".tmpl"))
                throw new MyArchException($"{path}: targets[{i}]: template \"{t.Template}\": a *.tmpl file in the plugin's folder");
            if (t.Merge is not ("" or "ini" or "prefs" or "lines" or "json"))
                throw new MyArchException($"{path}: targets[{i}]: merge = \"{t.Merge}\": \"ini\", \"prefs\", \"lines\" or \"json\"");
            if (t.Each.Count > 0 && (t.Output.StartsWith('/') || t.Output.StartsWith('~')))
                throw new MyArchException($"{path}: targets[{i}]: with each, output is relative to each place");
            if (t.Each.Count == 0 && !t.Output.StartsWith('/') && !t.Output.StartsWith("~/"))
                throw new MyArchException($"{path}: targets[{i}]: output starts with ~/ or / (or use each)");
            if (t.Output.Split('/').Contains(".."))
                throw new MyArchException($"{path}: targets[{i}]: output can't go up (..)");
            foreach (var e in t.Each)
            {
                // The part before the first wildcard is where every place is.
                var fixedPart = e;
                var w = e.IndexOfAny(['*', '?', '[']);
                if (w >= 0) fixedPart = Paths.Dir(e[..(w + 1)]);
                fixedPart = Paths.ExpandHome(fixedPart);
                if (Reserved(fixedPart) || Reserved(Paths.Join(fixedPart, "x")))
                    throw new MyArchException($"{path}: targets[{i}]: each {e} is in myarch's own folders");
            }
            if (t.Each.Count == 0 && Reserved(Paths.ExpandHome(t.Output)))
                throw new MyArchException($"{path}: targets[{i}]: {t.Output} is myarch's own");
            if (!File.Exists(Paths.Join(Dir, t.Template)))
                throw new MyArchException($"{path}: targets[{i}]: open {Paths.Join(Dir, t.Template)}: no such file or directory");
        }
        // Commands must say what they run (see Capabilities).
        try
        {
            ComputeCapabilities();
        }
        catch (MyArchException e)
        {
            throw new MyArchException($"{path}: {e.Message}");
        }
    }
}

public sealed class Target
{
    /// <summary>
    /// Template file name inside the plugin directory. Files named _*.tmpl
    /// hold functions (func … end) every template and command of the plugin
    /// can call.
    /// </summary>
    public string Template = "";
    /// <summary>Output path; "~/" is expanded.</summary>
    public string Output = "";
    /// <summary>
    /// Reload is a shell command run once after any of the plugin's files
    /// changed. It is rendered as a template too.
    /// </summary>
    public string Reload = "";
    /// <summary>
    /// Merge = "ini": the app writes this file too (qt6ct.conf, kdeglobals).
    /// myarch only manages the keys the template renders and keeps the app's
    /// own; only a change to its keys counts as an edit. "prefs" is the same
    /// for Firefox's user.js (user_pref("name", value); lines); "lines" makes
    /// sure the template's lines are in the file (an @import in someone's
    /// userChrome.css), at the top when missing; "json" sets the template's
    /// leaves in a JSON object (Chromium's Preferences).
    /// </summary>
    public string Merge = "";
    /// <summary>
    /// Each: glob patterns of a file that marks a place ("~/.config/mozilla/
    /// firefox/*/prefs.js": a Firefox profile). The target is written into
    /// every such directory, Output being relative to it; templates see the
    /// directory as place.
    /// </summary>
    public List<string> Each = [];
    /// <summary>
    /// Busy: a file, relative to each place, that says its app is running
    /// ("../SingletonLock" for a Chromium profile); then the file is left for
    /// the next apply, since the app would write its own copy back.
    /// </summary>
    public string Busy = "";
}

public sealed class Check
{
    /// <summary>Name and Run are rendered as templates, so the name can be translated (t "…") and the command can use settings.</summary>
    public string Name = "";
    /// <summary>Run is a shell command; exit status 0 means healthy. Its output is shown when it fails.</summary>
    public string Run = "";
    /// <summary>Timeout in seconds (default 15): a check that hangs has failed.</summary>
    public long Timeout;
    /// <summary>
    /// Session: the check needs the graphical session (Hyprland, the bar,
    /// the user's PipeWire). Outside it (a TTY, SSH) it's skipped, never
    /// failed: a skipped check must not roll back a good update.
    /// </summary>
    public bool Session;
}

public sealed class Action
{
    /// <summary>Name, Run and Key are rendered as templates: the name can be translated, the command and key can come from settings.</summary>
    public string Name = "";
    /// <summary>
    /// Run is a shell command, shown next to the action so it can be learned.
    /// Empty for a keybinding that only makes sense as a key (a mouse drag,
    /// "hold to…").
    /// </summary>
    public string Run = "";
    /// <summary>
    /// Key is the keybinding that does the same, as the plugin binds it
    /// ("SUPER + SHIFT + M"). Listing it here is how the palette knows every
    /// keybinding: the list is always right.
    /// </summary>
    public string Key = "";
    /// <summary>Terminal: run it in a terminal, for commands that ask or print (myarch update).</summary>
    public bool Terminal;
    /// <summary>Keywords help find it: other words people use for it.</summary>
    public string Keywords = "";
}
