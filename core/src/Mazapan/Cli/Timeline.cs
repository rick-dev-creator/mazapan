using System.Globalization;
using Mazapan.Applying;
using Mazapan.Config;
using Mazapan.Plugins;
using Mazapan.Store;
using Mazapan.Themes;
using Mazapan.Updates;
using Mazapan.Util;
using Change = Mazapan.Applying.Timeline.Change;

namespace Mazapan.Cli;

/// <summary>
/// The desktop's timeline: every apply (what it changed, in words) and every
/// update, newest first, each with its own undo. Undoing an apply that isn't
/// the last one puts back only what it changed and is still as it left it
/// (a theme, a setting, a plugin on or off), through a new apply: nothing
/// after it is lost, and the undo is on the timeline too.
/// </summary>
public static partial class Program
{
    static int CmdTimeline(string[] args)
    {
        if (args.Length > 0 && args[0] == "undo") return CmdTimelineUndo(args[1..]);
        var fs = new Flags("timeline")
            .Bool("json", "everything as JSON, for the History panel")
            .Parse(args);
        var entries = TimelineEntries();
        if (fs.IsSet("json"))
        {
            Console.WriteLine(GoJson.Marshal(new Fields { { "version", 1 }, { "entries", entries } }));
            return 0;
        }
        if (entries.Count == 0) Console.WriteLine("Nothing yet.");
        foreach (var e in entries)
        {
            var time = DateTimeOffset.Parse((string)e["time"]!, CultureInfo.InvariantCulture).LocalDateTime;
            var undo = (string)e["undo"]! switch
            {
                "change" or "last" or "root" => "  (mazapan timeline undo " + e["id"] + ")",
                "rollback" => "  (mazapan rollback " + e["id"] + ")",
                "apps" => "  (mazapan apps undo " + ((string)e["id"]!)["apps-".Length..] + ")",
                _ => "",
            };
            Console.WriteLine($"{time:yyyy-MM-dd HH:mm}  {e["title"]}{Style.Dim}{undo}{Style.Reset}");
            var cs = (List<Fields>)e["changes"]!;
            if (cs.Count > 1) foreach (var c in cs) Console.WriteLine($"    {c["text"]}");
        }
        return 0;
    }

    /// <summary>A time as JavaScript's Date reads it (milliseconds and the offset).</summary>
    static string JsTime(DateTimeOffset t) => t.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    /// <summary>Every apply and update as the panel shows them, newest first.</summary>
    static List<Fields> TimelineEntries()
    {
        var found = Plugin.Discover(PluginDirs()).Plugins;
        Settings? now = null;
        try { now = Settings.Load(); } catch (MazapanException) { }
        var lang = Locale.Languages.Detect(now?.Language ?? "");
        var names = new Dictionary<string, (string Name, Locale.Catalog? Cat, Plugin? P)>();
        (string Name, Locale.Catalog? Cat, Plugin? P) Named(string id)
        {
            if (names.TryGetValue(id, out var n)) return n;
            var p = found.FirstOrDefault(x => x.Id == id);
            var cat = p == null ? null : Locale.Catalog.Load(p.Id, p.Dir, lang);
            return names[id] = (cat?.TryT("plugin.name") ?? p?.Meta.Name ?? id, cat, p);
        }
        string ThemeName(object? id)
        {
            if (id is not string s || s == "") return "";
            try { return Theme.Load(ThemeDirs(), s).Meta.Name is { Length: > 0 } n ? n : s; }
            catch (MazapanException) { return s; }
        }

        // Which plugin writes a file: by the targets' outputs.
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in found)
            foreach (var t in p.Targets)
                owners.TryAdd(Paths.ExpandHome(t.Output), p.Id);

        var out_ = new List<(DateTimeOffset, Fields)>();
        var snaps = Snapshots.List();
        var configs = Timeline.Configs(snaps, Settings.Path);
        for (var i = 0; i < configs.Count; i++)
        {
            var (s, before, after) = configs[i];
            var changes = before != null && after != null ? Timeline.Diff(before, after) : null;
            var list = new List<Fields>();
            foreach (var c in changes ?? [])
            {
                var f = new Fields { { "kind", c.Kind }, { "plugin", c.Plugin }, { "key", c.Key }, { "from", Value(c.From) }, { "to", Value(c.To) } };
                if (c.Plugin != "") f.Add("plugin_name", Named(c.Plugin).Name);
                if (c.Kind == "setting")
                {
                    var (_, cat, p) = Named(c.Plugin);
                    f.Add("label", cat?.TryT($"setting.{c.Key}") ?? p?.SettingsInfo.GetValueOrDefault(c.Key)?.Label ?? SettingInfo.LabelOf(c.Key));
                    // What "not set" meant: the plugin's default.
                    f.Add("default", p != null && p.Settings.TryGetValue(c.Key, out var d) ? Value(d) : null);
                }
                if (c.Kind == "theme")
                {
                    f.Add("from_name", ThemeName(c.From));
                    f.Add("to_name", ThemeName(c.To));
                }
                f.Add("current", now != null && StillSo(c, now, found) && Valid(c, found) == null);
                f.Add("text", Describe(c, f));
                list.Add(f);
            }
            // What undo can do: put back what it changed that's still so; a
            // change of files only, the last apply's.
            // The files, by plugin: added, changed, removed.
            var byPlugin = s.Files.GroupBy(x => owners.GetValueOrDefault(x.Path, ""))
                .OrderBy(g => g.Key == "" ? 1 : 0).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new Fields
                {
                    { "plugin", g.Key },
                    { "plugin_name", g.Key == "" ? "" : Named(g.Key).Name },
                    { "added", g.Count(x => x.Before == "") },
                    { "removed", g.Count(x => x.Before != "" && x.After == "") },
                    { "changed", g.Count(x => x.Before != "" && x.After != "") },
                }).ToList();
            // Files only, the last apply's: as root when it wrote system files
            // or installed packages (in a terminal, for the password).
            var asRoot = s.Files.Any(e => AsRoot.IsSystem(e.Path)) || s.Packages.Count > 0;
            var undo = list.Any(f => (bool)f["current"]! && Invertible(f)) ? "change"
                : i == 0 && changes != null && list.Count == 0 && !s.Pending ? (asRoot ? "root" : "last") : "none";
            out_.Add((s.Time, new Fields
            {
                { "id", s.ID },
                { "kind", "apply" },
                { "time", JsTime(s.Time) },
                { "what", s.What },
                // An agent's (through the MCP server), by its name; "" the person's.
                { "by", s.By },
                { "title", list.Count > 0 ? string.Join("; ", list.Select(f => f["text"]))
                    : changes == null ? "an earlier change (what it was isn't kept)"
                    : "files of " + string.Join(", ", byPlugin.Select(g => (string)g["plugin_name"]! is { Length: > 0 } n ? n : "others")) },
                { "files", s.Files.Count },
                { "by_plugin", byPlugin },
                { "packages", s.Packages },
                { "known", changes != null },
                { "changes", list },
                { "undo", undo },
            }));
        }
        // Rollback goes from the newest update back: only the newest live one
        // is offered (an older one: roll the newer ones back first).
        var newestLive = History.List().FirstOrDefault(x => x.Live() && x.Outcome != Outcomes.InProgress)?.ID;
        foreach (var r in History.List())
        {
            var when = r.Started == default ? r.Finished : r.Started;
            out_.Add((when, new Fields
            {
                { "id", r.ID },
                { "kind", "update" },
                { "time", JsTime(when) },
                { "what", "update" },
                { "title", $"update: {Plural(r.Changes.Count, "package", "packages")}, {r.Outcome}" },
                { "packages_changed", r.Changes.Count },
                { "outcome", r.Outcome },
                { "note", r.Note },
                { "changes", new List<Fields>() },
                { "undo", r.ID == newestLive && r.Changes.Count > 0 ? "rollback" : "none" },
            }));
        }
        // Apps installed or removed from the catalog (mazapan apps): undone by
        // doing the other.
        var txs = AppsLedger.List(AppsState());
        var appsNow = txs.Count > 0 ? AppsNow.Read() : null;
        var catalog = txs.Count > 0 ? AppsCatalog(false).Apps : [];
        foreach (var tx in txs)
            out_.Add((tx.Time, new Fields
            {
                { "id", "apps-" + tx.Id },
                { "kind", "apps" },
                { "time", JsTime(tx.Time) },
                { "what", tx.Action },
                { "title", $"{(tx.Action == "install" ? "installed" : "removed")}: {string.Join(", ", tx.Names)}" },
                { "action", tx.Action },
                { "names", tx.Names },
                { "apps", tx.Apps },
                { "packages_count", tx.Packages.Count + tx.Flatpaks.Count },
                { "changes", new List<Fields>() },
                { "undo", AppsUndoable(tx, catalog, appsNow!) ? "apps" : "none" },
            }));

        // System snapshots (snapper, btrfs): a package change's before and
        // after as one entry; the "before" a checkpoint, in the boot menu
        // with hw-checkpoints, and restorable from here.
        var machine = Hardware.ThisMachine.Get();
        var inMenu = CheckpointsInMenu();
        var bootPossible = machine.Bootloader == "grub";
        foreach (var (pre, post, when, what) in SystemSnapshots())
            out_.Add((when, new Fields
            {
                { "id", post > 0 ? $"snapshot-{pre}-{post}" : $"snapshot-{pre}" },
                { "kind", "snapshot" },
                { "time", JsTime(when) },
                { "what", what },
                { "title", $"system snapshot: {what}" },
                { "pre", pre },
                { "post", post },
                { "bootable", inMenu.Contains(pre.ToString(CultureInfo.InvariantCulture)) },
                // The boot menu could have them (hw-checkpoints).
                { "boot_possible", bootPossible },
                { "boot_on_root", machine.BootOnRoot },
                { "changes", new List<Fields>() },
                { "undo", "none" },
            }));
        return out_.OrderByDescending(x => x.Item1).Select(x => x.Item2).ToList();
    }

    /// <summary>The checkpoints in the boot menu (hw-checkpoints writes which), by snapshot number.</summary>
    static HashSet<string> CheckpointsInMenu()
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Checkpoints.Checkpoints.MenuState));
            return doc.RootElement.GetProperty("entries").EnumerateArray()
                .Select(e => e.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "").ToHashSet();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>
    /// Snapper's snapshots of /, from each one's info.xml (or, without the
    /// rights to /.snapshots, the list hw-checkpoints keeps):
    /// (before, after or 0, when, what).
    /// </summary>
    static List<(long Pre, long Post, DateTimeOffset When, string What)> SystemSnapshots()
    {
        var out_ = new List<(long, long, DateTimeOffset, string)>();
        // /.snapshots is root's: the list root keeps stands in (hw-checkpoints).
        var items = (Checkpoints.Checkpoints.Raw() ?? []).Select(x => (x.N, x.Type, x.Pre, Date: x.Date.ToLocalTime(), x.What)).ToList();
        var paired = items.Where(x => x.Type == "post").Select(x => x.Pre).ToHashSet();
        foreach (var x in items)
        {
            if (x.Type == "post")
            {
                var pre = items.FirstOrDefault(y => y.N == x.Pre);
                out_.Add((x.Pre, x.N, x.Date, pre.What != "" ? pre.What : x.What));
            }
            else if (!(x.Type == "pre" && paired.Contains(x.N)))
                out_.Add((x.N, 0, x.Date, x.What));
        }
        return out_;
    }

    static object? Value(object? v) => v == null ? null : JsonValue(v);

    static bool Invertible(Fields f) => (string)f["kind"]! switch
    {
        "theme" => f["from"] is string t && t != "",
        "accent" or "font" or "on" or "off" or "setting" => true,
        _ => false,
    };

    /// <summary>StillSo: config.toml still has what the change left.</summary>
    static bool StillSo(Change c, Settings now, List<Plugin> found)
    {
        var p = found.FirstOrDefault(x => x.Id == c.Plugin);
        return c.Kind switch
        {
            "theme" => now.Theme == (string?)c.To,
            "accent" => now.Accent == ((string?)c.To ?? ""),
            "language" => now.Language == ((string?)c.To ?? ""),
            "font" => c.Key switch
            {
                "ui" => now.FontUI == ((string?)c.To ?? ""),
                "mono" => now.FontMono == ((string?)c.To ?? ""),
                _ => now.FontSize == (c.To is double d ? d : c.To is long l ? l : 0),
            },
            // Hardware plugins are the person's, through mazapan hardware.
            "on" => p != null && p.Hardware == null && now.IsOn(p),
            "off" => p != null && p.Hardware == null && !now.IsOn(p),
            "setting" => p != null && Timeline.Same(now.For(c.Plugin)?.GetValueOrDefault(c.Key), c.To),
            _ => false,
        };
    }

    /// <summary>
    /// Valid: why putting this part back can't be done today (null: it can):
    /// a plugin that's gone, a setting it no longer has or a value it no
    /// longer takes, a theme that's gone, a hardware plugin (the person's,
    /// with mazapan apply --system).
    /// </summary>
    static string? Valid(Change c, List<Plugin> found)
    {
        var p = found.FirstOrDefault(x => x.Id == c.Plugin);
        switch (c.Kind)
        {
            case "theme":
                if (c.From is not string t || t == "") return "there was no theme before it";
                return Theme.List(ThemeDirs()).Contains(t) ? null : $"theme {t} isn't there any more";
            case "accent":
                return c.From is null or string { Length: 7 } && (c.From is null || ((string)c.From).StartsWith('#')) ? null : "the accent before it isn't a color";
            case "font":
                return null;
            case "on" or "off":
                if (p == null) return $"{c.Plugin} isn't there any more";
                return p.Hardware != null || p.Targets.Any(x => x.System) ? $"{c.Plugin} is a hardware plugin: mazapan hardware" : null;
            case "setting":
                if (p == null) return $"{c.Plugin} isn't there any more";
                if (p.Hardware != null || p.Targets.Any(x => x.System)) return $"{c.Plugin} is a hardware plugin: mazapan apply --system";
                if (!p.Settings.ContainsKey(c.Key)) return $"{c.Plugin} has no setting {c.Key} any more";
                if (c.From == null) return null;
                try
                {
                    p.Resolve(new Dictionary<string, object> { [c.Key] = c.From });
                    return null;
                }
                catch (MazapanException e)
                {
                    return e.Message;
                }
            default:
                return "change it in config.toml";
        }
    }

    static string Describe(Change c, Fields f)
    {
        string V(object? v) => v != null ? TomlWriter.Value(v) : f["default"] is { } d ? $"default ({GoJson.Marshal(d)})" : "default";
        return c.Kind switch
        {
            "theme" => $"theme {f["from_name"]} → {f["to_name"]}".Replace("theme  →", "theme →"),
            "accent" => $"accent {(c.From == null ? "the theme's" : c.From)} → {(c.To == null ? "the theme's" : c.To)}",
            "language" => $"language {c.From ?? "the system's"} → {c.To ?? "the system's"}",
            "font" => $"{(c.Key == "ui" ? "font" : c.Key == "mono" ? "monospace font" : "text size")} {c.From ?? "the theme's"} → {c.To ?? "the theme's"}",
            "on" => $"{f["plugin_name"]} turned on",
            "off" => $"{f["plugin_name"]} turned off",
            "setting" => $"{f["plugin_name"]}: {f["label"]} {V(c.From)} → {V(c.To)}",
            "catalog" => $"catalog added: {c.To}",
            "catalog-removed" => $"catalog removed: {c.From}",
            _ => c.Kind,
        };
    }

    static int CmdTimelineUndo(string[] args)
    {
        // -y before or after the IDs.
        args = [.. args.Where(a => a.StartsWith('-')), .. args.Where(a => !a.StartsWith('-'))];
        var fs = new Flags("timeline undo")
            .Bool("y", "don't ask")
            .Parse(args);
        if (fs.Rest.Count == 0) throw new MazapanException("usage: mazapan timeline undo ID… [-y]");
        if (fs.Rest.Count == 1) return UndoOne(fs.Rest[0], fs.IsSet("y"));
        // Several (an agent's changes at once): newest first, each on what the
        // one after it left; one that can't is said, the rest go on.
        if (!fs.IsSet("y")) throw new MazapanException("several at once: with -y (mazapan timeline lists them)");
        var order = Snapshots.List().Select(s => s.ID).ToList();
        var failed = 0;
        foreach (var id in fs.Rest.Distinct().OrderBy(id => order.IndexOf(id) is var i && i < 0 ? int.MaxValue : i))
        {
            try
            {
                if (UndoOne(id, true) != 0) failed++;
            }
            catch (MazapanException e)
            {
                Console.Error.WriteLine($"mazapan: {id}: {e.Message}");
                failed++;
            }
        }
        return failed == 0 ? 0 : 1;
    }

    static int UndoOne(string id, bool yes)
    {
        var snaps = Snapshots.List();
        var at = snaps.FindIndex(s => s.ID == id);
        if (at < 0) throw new MazapanException($"no apply {id} (mazapan timeline)");
        var (s, before, after) = Timeline.Configs(snaps, Settings.Path)[at];
        var changes = before != null && after != null ? Timeline.Diff(before, after) : [];

        // Files only: the last apply's, as undo does.
        if (changes.Count == 0)
        {
            if (at != 0) throw new MazapanException($"{id} ({s.What}) changed files only, and applies came after it: only the last one's files can be put back (mazapan undo)");
            var undoArgs = new List<string> { "--id=" + id };
            if (yes) undoArgs.Add("-y");
            return CmdUndo([.. undoArgs]);
        }

        var now = Settings.Load();
        var found = Plugin.Discover(PluginDirs()).Plugins;
        var apply = new List<string>();
        var left = new List<string>();
        foreach (var c in changes)
        {
            if (Valid(c, found) is { } why)
            {
                left.Add($"{Label(c)}: {why}");
                continue;
            }
            if (!StillSo(c, now, found))
            {
                left.Add($"{Label(c)}: changed since, left as it is");
                continue;
            }
            switch (c.Kind)
            {
                case "theme" when c.From is string t && t != "":
                    apply.Add("--theme=" + t);
                    break;
                case "accent":
                    apply.Add("--accent=" + ((string?)c.From ?? "theme"));
                    break;
                case "font":
                    apply.Add($"--font-{(c.Key == "ui" ? "ui" : c.Key == "mono" ? "mono" : "size")}=" + (c.From == null ? "theme" : Convert.ToString(c.From, System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case "on":
                    apply.Add("--disable=" + c.Plugin);
                    break;
                case "off":
                    apply.Add("--enable=" + c.Plugin);
                    break;
                case "setting":
                    apply.Add(c.From == null ? $"--reset={c.Plugin}.{c.Key}" : $"--set={c.Plugin}.{c.Key}={TomlWriter.Value(c.From)}");
                    break;
                default:
                    left.Add($"{Label(c)}: change it in config.toml");
                    break;
            }
        }
        Header($"Undo {s.What} ({s.ID})");
        foreach (var a in apply) Console.WriteLine($"  {a}");
        foreach (var l in left) Console.WriteLine($"  {Style.Amber}{l}{Style.Reset}");
        if (apply.Count == 0) throw new MazapanException("nothing of it is still as it left it: nothing to undo");
        if (!yes)
        {
            if (!IsTerminal(0)) throw new MazapanException("no terminal to ask on: undo with -y");
            if (!Confirm("Put these back as they were?")) return 0;
        }
        return CmdApply([.. apply]);
    }

    static string Label(Change c) => c.Kind switch
    {
        "setting" => $"{c.Plugin}.{c.Key}",
        "on" or "off" => $"{c.Plugin} turned {c.Kind}",
        _ => c.Kind,
    };
}
