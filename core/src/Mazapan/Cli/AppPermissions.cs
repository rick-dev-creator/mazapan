using Mazapan.Store;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// What a Flatpak app may reach, said plainly and changed with a switch
/// (Flatseal's model: the person's own overrides, `flatpak override
/// --user`, over what the app asks for; reset puts the app's own back). For
/// Flatpak apps only: an app from the repositories runs as the person, with
/// everything they can reach, and that's said rather than pretended.
/// </summary>
public static class FlatpakPermissions
{
    /// <summary>A switch: its key, the context group and value it is in `flatpak info --show-permissions`, and the override flags.</summary>
    public sealed record Kind(string Key, string Group, string Value, string On, string Off);

    public static readonly Kind[] Kinds =
    [
        new("network", "shared", "network", "--share=network", "--unshare=network"),
        new("sound", "sockets", "pulseaudio", "--socket=pulseaudio", "--nosocket=pulseaudio"),
        new("devices", "devices", "all", "--device=all", "--nodevice=all"),
        new("home", "filesystems", "home", "--filesystem=home", "--nofilesystem=home"),
        new("files", "filesystems", "host", "--filesystem=host", "--nofilesystem=host"),
        new("downloads", "filesystems", "xdg-download", "--filesystem=xdg-download", "--nofilesystem=xdg-download"),
        new("bluetooth", "features", "bluetooth", "--allow=bluetooth", "--disallow=bluetooth"),
    ];

    /// <summary>
    /// The switches as `flatpak info --show-permissions` gives them ([Context]
    /// key=value;value;). A filesystem counts with or without its :ro/:rw;
    /// "!home" is one taken away.
    /// </summary>
    public static Dictionary<string, bool> Parse(string info)
    {
        var context = new Dictionary<string, HashSet<string>>();
        var inContext = false;
        foreach (var raw in info.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[')) { inContext = line == "[Context]"; continue; }
            var eq = line.IndexOf('=');
            if (!inContext || eq <= 0) continue;
            context[line[..eq]] = line[(eq + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Split(':')[0]).ToHashSet();
        }
        return Kinds.ToDictionary(k => k.Key, k => context.TryGetValue(k.Group, out var vs) && vs.Contains(k.Value) && !vs.Contains("!" + k.Value));
    }

    /// <summary>`flatpak override --user` for one switch.</summary>
    public static string[] Override(string app, string key, bool on)
    {
        var k = Kinds.FirstOrDefault(x => x.Key == key)
            ?? throw new MazapanException($"no permission \"{key}\": one of {string.Join(", ", Kinds.Select(x => x.Key))}");
        return ["override", "--user", on ? k.On : k.Off, app];
    }
}

public static partial class Program
{
    /// <summary>The Flatpak an id names: an app of the catalog's, or a Flatpak id itself.</summary>
    static string FlatpakOf(string id, List<App> apps)
    {
        if (apps.FirstOrDefault(a => a.Id == id) is { } a)
            return a.Flatpak != "" ? a.Flatpak : throw new MazapanException($"{id} isn't a Flatpak app: from the repositories, an app reaches what you can (no per-app permissions)");
        if (!AppCatalog.IsFlatpak(id)) throw new MazapanException($"no app \"{id}\" (an id from mazapan apps, or a Flatpak id)");
        return id;
    }

    /// <summary>mazapan apps permissions ID [--json]: what a Flatpak app may reach.</summary>
    static int AppsPermissions(List<App> apps, List<string> ids, bool json)
    {
        if (ids.Count != 1) throw new MazapanException("usage: mazapan apps permissions ID [--json]");
        var app = FlatpakOf(ids[0], apps);
        var (code, out_, err) = AppsCapture("flatpak", "info", "--show-permissions", app);
        if (code != 0) throw new MazapanException($"{app}: {err.Trim()}");
        var now = FlatpakPermissions.Parse(out_);
        var (oc, overrides, _) = AppsCapture("flatpak", "override", "--user", "--show", app);
        var changed = oc == 0 && overrides.Contains('=');
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "app", app },
                { "permissions", FlatpakPermissions.Kinds.Select(k => new Fields { { "key", k.Key }, { "on", now[k.Key] } }).ToList() },
                { "changed", changed },
            }));
            return 0;
        }
        foreach (var k in FlatpakPermissions.Kinds)
            Console.WriteLine($"  {(now[k.Key] ? Style.Green + "on " : Style.Dim + "off")}{Style.Reset} {k.Key}");
        if (changed) Console.WriteLine($"\nChanged by you: mazapan apps permit {ids[0]} reset puts the app's own back.");
        return 0;
    }

    /// <summary>mazapan apps permit ID KEY on|off, or ID reset.</summary>
    static int AppsPermit(List<App> apps, List<string> args)
    {
        if (args is not ([_, "reset"] or [_, _, "on" or "off"]))
            throw new MazapanException($"usage: mazapan apps permit ID {string.Join("|", FlatpakPermissions.Kinds.Select(k => k.Key))} on|off, or ID reset");
        var app = FlatpakOf(args[0], apps);
        string[] cmd = args[1] == "reset" ? ["override", "--user", "--reset", app] : FlatpakPermissions.Override(app, args[1], args[2] == "on");
        var (code, _, err) = AppsCapture("flatpak", cmd);
        if (code != 0) throw new MazapanException($"flatpak: {err.Trim()}");
        // Running now, it keeps what it had until it starts again.
        Console.WriteLine(args[1] == "reset" ? $"{app}: its own permissions again." : $"{app}: {args[1]} {args[2]} (from its next start).");
        return 0;
    }
}
