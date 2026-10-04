using System.Text.RegularExpressions;
using Mazapan.Util;
using Tomlyn.Model;

namespace Mazapan.Plugins;

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
        /// <summary>Categories, for the Plugins panel: bar, panel, theme, window, hardware, tools, agent.</summary>
        public List<string> Categories = [];
        /// <summary>Optional: off until turned on, as hardware plugins are (the extras: a screensaver, reminders…).</summary>
        public bool Optional;
    }

    /// <summary>Off until turned on (enabled_plugins), rather than on until turned off (disabled_plugins).</summary>
    public bool OffByDefault => Hardware != null || Meta.Optional;

    public MetaTable Meta { get; } = new();

    /// <summary>Packages the plugin needs at runtime. Checked, not installed (yet).</summary>
    public List<string> Pacman { get; set; } = [];

    /// <summary>
    /// Settings are the plugin's knobs with their defaults. People override
    /// them in config.toml under [plugins.&lt;id&gt;]; templates read the
    /// result as settings.
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = [];

    /// <summary>What each setting is: its label, description, choices, range, kind (see SettingInfo).</summary>
    public Dictionary<string, SettingInfo> SettingsInfo { get; } = [];

    /// <summary>
    /// Targets are files the plugin generates. The core renders and writes
    /// them; plugins never touch the filesystem themselves.
    /// </summary>
    public List<Target> Targets { get; } = [];

    /// <summary>
    /// Checks say whether what the plugin is responsible for still works.
    /// `mazapan doctor` runs them on demand; `mazapan update` runs them after
    /// updating and rolls back when one fails.
    /// </summary>
    public List<Check> Checks { get; } = [];

    /// <summary>
    /// Actions are what the plugin lets you do: the command palette lists
    /// every plugin's, each with the command it runs and its keybinding.
    /// </summary>
    public List<Action> Actions { get; } = [];

    /// <summary>
    /// Coverage says which apps the plugin themes, so `mazapan coverage` can
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

    /// <summary>
    /// Hardware: the machines the plugin is for ([hardware]); null for a plugin
    /// for every machine. A hardware plugin is off until the person turns it
    /// on (mazapan hardware lists the ones for this machine), and does nothing
    /// on a machine it isn't for, even when on (a config.toml shared with
    /// another computer).
    /// </summary>
    public Hardware.Rules? Hardware { get; set; }

    public string Id => Meta.Id;

    /// <summary>Its categories: the ones it says, or what its files make it.</summary>
    public List<string> CategoriesOrGuess()
    {
        if (Meta.Categories.Count > 0) return Meta.Categories;
        var outs = Targets.Select(t => t.Output).ToList();
        if (Hardware != null) return ["hardware"];
        if (outs.Any(o => o.Contains("/quickshell/mazapan/widgets/"))) return ["bar"];
        if (outs.Any(o => o.Contains("/quickshell/mazapan/panels/"))) return ["panel"];
        if (Id.StartsWith("theme-") || Id == "wallpaper") return ["theme"];
        if (outs.Any(o => o.Contains("/hypr/"))) return ["window"];
        return ["tools"];
    }

    /// <summary>Where a plugin may put a system file (system = true): drop-in folders.</summary>
    public static readonly string[] SystemDirs =
    [
        "/etc/modprobe.d", "/etc/modules-load.d", "/etc/mkinitcpio.conf.d", "/etc/udev/rules.d", "/etc/udev/hwdb.d", "/etc/sysctl.d",
        "/etc/tmpfiles.d", "/etc/systemd/logind.conf.d", "/etc/systemd/sleep.conf.d", "/etc/X11/xorg.conf.d",
        "/etc/chromium/policies/managed", "/etc/opt/chrome/policies/managed", "/etc/brave/policies/managed",
        "/etc/opt/edge/policies/managed",
        // grub-btrfs's daemon (snapshots in the boot menu): what it passes a snapshot's kernel.
        "/etc/systemd/system/grub-btrfsd.service.d",
        // The login screen: greetd's configuration (its own is left alone), how it's
        // started, and its own PAM service (a new one: the system's aren't touched).
        "/etc/greetd", "/etc/systemd/system/greetd.service.d", "/etc/pam.d",
        // The firewall: ufw started at every start, while mazapan's drop-in is there.
        "/etc/systemd/system/ufw.service.d",
        // Tailscale: its daemon started at every start, the person made its operator.
        "/etc/systemd/system/tailscaled.service.d",
        // Docker: its socket started at every start.
        "/etc/systemd/system/docker.socket.d",
        // Hibernation: what a battery about to die does.
        "/etc/UPower/UPower.conf.d",
        // The boot menu in the theme: a script grub-mkconfig runs.
        "/etc/grub.d",
        // The boot screen in the theme: Plymouth's theme, a folder only it writes.
        "/usr/share/plymouth/themes/mazapan",
        // Units of mazapan's own (mazapan-*.service, .timer): downloading ahead.
        "/etc/systemd/system",
    ];

    static readonly HashSet<string> KnownToolkits =
        ["terminal", "gtk4", "gtk3", "qt6", "qt5", "electron", "chromium", "firefox", "flatpak", "web"];

    [GeneratedRegex(@"^\{\{\s*settings\.([a-z0-9_]+)\s*\}\}\z")]
    private static partial Regex KeyFromSetting();

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
                throw new MazapanException($"[plugins.{Id}] {k}: unknown setting (known: {Keys(Settings)})");
            out_[k] = Coerce(def, v) ?? throw new MazapanException(
                $"[plugins.{Id}] {k} = {Show(v)}: want a {GoType(def)} like the default {Show(def)}");
            if (SettingsInfo.TryGetValue(k, out var info) && info.Check(out_[k]) is { } why)
                throw new MazapanException($"[plugins.{Id}] {k} {why}");
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
                        throw new MazapanException($"{path}: id \"{p.Id}\", but its folder is \"{id}\": they must match");
                    byId[id] = p;
                }
                catch (MazapanException e)
                {
                    broken[id] = e.Message;
                }
            }
        }
        return (byId.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList(), broken);
    }

    /// <summary>
    /// Reserved says whether path is mazapan's own: its config, plugins.lock,
    /// the plugins and themes it loads, what it remembers. No plugin writes
    /// there (bin/, for the scripts plugins ship, is the exception).
    /// </summary>
    public static bool Reserved(string path)
    {
        var home = Paths.Home;
        path = Paths.Clean(path);
        foreach (var r in new[] { ".config/mazapan", ".local/state/mazapan", ".local/share/mazapan" })
        {
            var root = Paths.Join(home, r);
            if (path == root || path.StartsWith(root + "/"))
                return !path.StartsWith(Paths.Join(home, ".local/share/mazapan/bin") + "/");
        }
        return false;
    }

    static Plugin LoadManifest(string path)
    {
        var table = Toml.ReadFile(path) ?? throw new MazapanException($"{path}: no such file");
        var p = new Plugin { Dir = Paths.Dir(path) };
        var r = new TomlReader(table, path);
        var meta = r.Sub("plugin");
        p.Meta.Id = meta.String("id");
        p.Meta.Name = meta.String("name");
        p.Meta.Version = meta.String("version");
        p.Meta.Api = meta.Int("api");
        p.Meta.Description = meta.String("description");
        p.Meta.Requires = meta.Strings("requires");
        p.Meta.Categories = meta.Strings("categories");
        p.Meta.Optional = meta.Bool("optional");
        foreach (var c in p.Meta.Categories)
            if (!Install.CatalogIndex.KnownCategories.Contains(c))
                throw new MazapanException($"{path}: [plugin] category \"{c}\": one of {string.Join(", ", Install.CatalogIndex.KnownCategories)}");
        p.Pacman = r.Sub("packages").Strings("pacman");
        if (r.Raw("settings") is { } settings)
        {
            var comments = SettingInfo.Comments(File.ReadAllText(path));
            foreach (var (k, v) in settings)
            {
                SettingInfo info;
                if (v is TomlTable t && t.ContainsKey("default"))
                    (p.Settings[k], info) = SettingInfo.Parse(path, k, t);
                else
                {
                    p.Settings[k] = v;
                    info = new SettingInfo { Kind = SettingInfo.KindOf(k, v) };
                }
                if (info.Description == "") info.Description = comments.GetValueOrDefault(k, "");
                if (info.Label == "") info.Label = SettingInfo.LabelOf(k);
                p.SettingsInfo[k] = info;
            }
        }
        foreach (var t in r.Array("targets"))
            p.Targets.Add(new Target
            {
                Template = t.String("template"),
                Output = t.String("output"),
                Reload = t.String("reload"),
                Merge = t.String("merge"),
                Each = t.Strings("each"),
                Busy = t.String("busy"),
                System = t.Bool("system"),
                Reboot = t.Bool("reboot"),
            });
        foreach (var c in r.Array("checks"))
            p.Checks.Add(new Check
            {
                Name = c.String("name"),
                Run = c.String("run"),
                Timeout = c.Int("timeout"),
                Session = c.Bool("session"),
                Before = c.Bool("before_system"),
            });
        foreach (var a in r.Array("actions"))
            p.Actions.Add(new Action
            {
                Name = a.String("name"),
                Run = a.String("run"),
                Key = a.String("key"),
                Terminal = a.Bool("terminal"),
                Keywords = a.String("keywords"),
                Home = a.Int("home"),
                Glyph = a.String("glyph"),
                Label = a.String("label"),
                Confirm = a.Bool("confirm"),
                KeySetting = KeyFromSetting().Match(a.String("key")) is { Success: true } km ? km.Groups[1].Value : "",
            });
        var cov = r.Sub("coverage");
        p.Coverage.Apps = cov.Strings("apps");
        p.Coverage.Toolkits = cov.Strings("toolkits");
        if (r.Has("hardware"))
        {
            var hw = r.Sub("hardware");
            p.Hardware = new Hardware.Rules
            {
                Vendor = hw.Strings("vendor"),
                Product = hw.Strings("product"),
                Board = hw.Strings("board"),
                Pci = hw.Strings("pci"),
                Usb = hw.Strings("usb"),
                Gpu = hw.Strings("gpu"),
                Input = hw.Strings("input"),
                Modules = hw.Strings("modules"),
                Gpus = hw.Int("gpus"),
                Filesystem = hw.Strings("filesystem"),
                Bootloader = hw.Strings("bootloader"),
                BootOnRoot = hw.Bool("boot_on_root"),
                Live = hw.Bool("live"),
                Any_ = hw.Bool("any"),
            };
            if (p.Hardware.Empty) throw new MazapanException($"{path}: [hardware] needs at least one rule");
            if (p.Hardware.BadPatterns().FirstOrDefault() is { } bad)
                throw new MazapanException($"{path}: [hardware] \"{bad}\" isn't a pattern (a literal [ is \\[)");
        }
        r.Done();
        p.Validate(path);
        return p;
    }

    void Validate(string path)
    {
        if (Meta.Id == "") throw new MazapanException($"{path}: [plugin] id is required");
        if (!IdPattern().IsMatch(Meta.Id))
            throw new MazapanException($"{path}: [plugin] id \"{Meta.Id}\": lowercase letters, digits and dashes");
        if (Meta.Api != ApiVersion)
            throw new MazapanException($"{path}: api = {Meta.Api}, this core supports api = {ApiVersion}");
        try
        {
            Versions.Parse(Meta.Version);
        }
        catch (MazapanException e)
        {
            throw new MazapanException($"{path}: [plugin] {e.Message}");
        }
        foreach (var req in Meta.Requires)
        {
            Requirement r;
            try
            {
                r = Requirement.Parse(req);
            }
            catch (MazapanException e)
            {
                throw new MazapanException($"{path}: [plugin] {e.Message}");
            }
            if (r.Id == Meta.Id) throw new MazapanException($"{path}: [plugin] requires itself");
        }
        for (var i = 0; i < Checks.Count; i++)
        {
            if (Checks[i].Name == "" || Checks[i].Run == "")
                throw new MazapanException($"{path}: checks[{i}] needs name and run");
            if (Checks[i].Timeout < 0 || Checks[i].Timeout > 3600)
                throw new MazapanException($"{path}: checks[{i}]: timeout is seconds, up to 3600");
        }
        foreach (var t in Coverage.Toolkits)
            if (!KnownToolkits.Contains(t)) throw new MazapanException($"{path}: coverage: unknown toolkit \"{t}\"");
        for (var i = 0; i < Actions.Count; i++)
            if (Actions[i].Name == "" || (Actions[i].Run == "" && Actions[i].Key == ""))
                throw new MazapanException($"{path}: actions[{i}] needs a name, and run or key");
        var home = Paths.Home;
        for (var i = 0; i < Targets.Count; i++)
        {
            var t = Targets[i];
            if (t.Template == "" || t.Output == "")
                throw new MazapanException($"{path}: targets[{i}] needs template and output");
            // A template is a file of the plugin: never a path out of its
            // folder (a plugin from git reading ~/.ssh into its output).
            if (t.Template.Contains('/') || t.Template.StartsWith('.') || !t.Template.EndsWith(".tmpl"))
                throw new MazapanException($"{path}: targets[{i}]: template \"{t.Template}\": a *.tmpl file in the plugin's folder");
            if (t.Merge is not ("" or "ini" or "prefs" or "lines" or "json"))
                throw new MazapanException($"{path}: targets[{i}]: merge = \"{t.Merge}\": \"ini\", \"prefs\", \"lines\" or \"json\"");
            if (t.Each.Count > 0 && (t.Output.StartsWith('/') || t.Output.StartsWith('~')))
                throw new MazapanException($"{path}: targets[{i}]: with each, output is relative to each place");
            if (t.Each.Count == 0 && !t.Output.StartsWith('/') && !t.Output.StartsWith("~/"))
                throw new MazapanException($"{path}: targets[{i}]: output starts with ~/ or / (or use each)");
            if (t.Output.Split('/').Contains(".."))
                throw new MazapanException($"{path}: targets[{i}]: output can't go up (..)");
            foreach (var e in t.Each)
            {
                // The part before the first wildcard is where every place is.
                var fixedPart = e;
                var w = e.IndexOfAny(['*', '?', '[']);
                if (w >= 0) fixedPart = Paths.Dir(e[..(w + 1)]);
                fixedPart = Paths.ExpandHome(fixedPart);
                if (Reserved(fixedPart) || Reserved(Paths.Join(fixedPart, "x")))
                    throw new MazapanException($"{path}: targets[{i}]: each {e} is in mazapan's own folders");
            }
            if (t.System)
            {
                // A system file is mazapan's by its name, in a folder made for
                // drop-ins: never a file the system or another package owns.
                if (!SystemDirs.Contains(Paths.Dir(t.Output)) || !Paths.Base(t.Output).StartsWith("mazapan") || t.Each.Count > 0 || t.Merge != ""
                    || (Paths.Dir(t.Output) == "/etc/pam.d" && Paths.Base(t.Output) != "mazapan-greetd"))
                    throw new MazapanException($"{path}: targets[{i}]: a system file is /etc/…/mazapan*, in one of: {string.Join(", ", SystemDirs)}");
            }
            else if (t.Output.StartsWith('/') && !t.Output.StartsWith(Paths.Home + "/"))
                throw new MazapanException($"{path}: targets[{i}]: {t.Output} is outside your home: set system = true (written with sudo)");
            if (t.Reboot && !t.System)
                throw new MazapanException($"{path}: targets[{i}]: reboot is for system files");
            if (t.Each.Count == 0 && Reserved(Paths.ExpandHome(t.Output)))
                throw new MazapanException($"{path}: targets[{i}]: {t.Output} is mazapan's own");
            if (!File.Exists(Paths.Join(Dir, t.Template)))
                throw new MazapanException($"{path}: targets[{i}]: open {Paths.Join(Dir, t.Template)}: no such file or directory");
        }
        // What goes into a file as root comes from the plugin, and numbers and
        // switches: never text, which any program can put in config.toml.
        if (Targets.Any(t => t.System) && Settings.FirstOrDefault(kv => kv.Value is string or Tomlyn.Model.TomlArray or Tomlyn.Model.TomlTable) is { Key: { } textKey })
            throw new MazapanException($"{path}: [settings] {textKey}: a plugin with system files has only number and true/false settings");
        foreach (var p in Pacman)
            if (!Applying.AsRoot.IsPackage(p)) throw new MazapanException($"{path}: [packages] \"{p}\" isn't a package name");
        // Commands must say what they run (see Capabilities).
        try
        {
            ComputeCapabilities();
        }
        catch (MazapanException e)
        {
            throw new MazapanException($"{path}: {e.Message}");
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
    /// mazapan only manages the keys the template renders and keeps the app's
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
    /// <summary>
    /// System: a file outside your home (/etc/modprobe.d/mazapan-…), written as
    /// root with sudo, only by `mazapan apply --system`; its reload runs as root.
    /// </summary>
    public bool System;
    /// <summary>Reboot: the change takes effect after a reboot (a kernel module option).</summary>
    public bool Reboot;
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
    /// <summary>
    /// Before (before_system): must pass before `apply --system` writes the
    /// plugin's system files (kernel headers before a module built from them).
    /// </summary>
    public bool Before;
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
    /// <summary>Terminal: run it in a terminal, for commands that ask or print (mazapan update).</summary>
    public bool Terminal;
    /// <summary>Keywords help find it: other words people use for it.</summary>
    public string Keywords = "";
    /// <summary>
    /// Home: on the palette's first screen (what opens before anything is
    /// typed), in this order; 0 isn't. A plugin's way in (its panel), not
    /// each thing it does: those are found by typing.
    /// </summary>
    public long Home;
    /// <summary>Its icon on that screen: a Nerd Font glyph.</summary>
    public string Glyph = "";
    /// <summary>Its name on that screen, short ("Theme"; Name is "Change the theme"); a template, as Name. Empty: Name.</summary>
    public string Label = "";
    /// <summary>Confirm: done only when asked twice (power off, reboot): one stray ↵ doesn't.</summary>
    public bool Confirm;
    /// <summary>
    /// KeySetting: the setting the key comes from (key = "{{ settings.X }}"),
    /// so Settings › Keys can change it; "" for a key that's fixed.
    /// </summary>
    public string KeySetting = "";
}
