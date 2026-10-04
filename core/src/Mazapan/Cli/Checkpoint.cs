using System.Globalization;
using System.Text;
using System.Text.Json;
using Mazapan.Checkpoints;
using Mazapan.Pacman;
using Mazapan.Updates;
using Mazapan.Util;

namespace Mazapan.Cli;

/// <summary>
/// mazapan checkpoint: the system as it was before a change, in words.
/// Which one this is (when started from one), which there are, what changed
/// since (for the person and an agent), and making one the main system.
/// The rest is for the system's own hooks (as root): kernels kept, the boot
/// menu written, the start told apart.
/// </summary>
public static partial class Program
{
    const int CheckpointEntries = 5;
    const int CheckpointDays = 7;

    static int CmdCheckpoint(string[] args)
    {
        var json = args.Contains("--json");
        var yes = args.Contains("-y") || args.Contains("--yes");
        int Opt(string name, int def)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var v) && v > 0 ? v : def;
        }
        var entries = Opt("--entries", CheckpointEntries);
        var days = Opt("--keep-days", CheckpointDays);
        // Words: what isn't a flag, nor the value of --entries / --keep-days.
        var words = args.Where((a, i) => !a.StartsWith('-') && !(i > 0 && args[i - 1] is "--entries" or "--keep-days")).ToList();
        var verb = words.FirstOrDefault() ?? "status";
        var arg = words.Skip(1).FirstOrDefault();
        switch (verb)
        {
            case "status": return CheckpointStatus(json);
            case "list": return CheckpointList(json);
            case "diagnose": return CheckpointDiagnose(arg);
            case "keep": return CheckpointKeep(null, entries, days, yes);
            case "restore":
                if (arg == null) throw new MazapanException("usage: mazapan checkpoint restore N");
                return CheckpointKeep(arg, entries, days, yes);
            case "save": NeedRoot("save"); Console.WriteLine(Checkpoints.Checkpoints.Save()); return 0;
            case "after-pacman":
                // pacman's hook: never fails the transaction, says what went wrong.
                NeedRoot("after-pacman");
                try
                {
                    Console.WriteLine(Checkpoints.Checkpoints.Save());
                    Console.WriteLine(Checkpoints.Checkpoints.Menu(entries, days));
                }
                catch (Exception e) when (e is MazapanException or IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine("checkpoints: " + e.Message);
                }
                return 0;
            case "menu": NeedRoot("menu"); Console.WriteLine(Checkpoints.Checkpoints.Menu(entries, days)); return 0;
            case "boot": NeedRoot("boot"); Console.WriteLine(Checkpoints.Checkpoints.Boot(entries, days)); return 0;
            default:
                Console.Error.Write("""
                    usage: mazapan checkpoint [status] [--json]
                           mazapan checkpoint list [--json]
                           mazapan checkpoint diagnose [N]
                           mazapan checkpoint keep [-y]        (on a checkpoint: it becomes the main system)
                           mazapan checkpoint restore N [-y]   (checkpoint N becomes the main system at the next start)

                    """);
                return 2;
        }
    }

    static void NeedRoot(string what)
    {
        if (GetEuid() != 0) throw new MazapanException($"checkpoint {what}: needs root (sudo)");
    }

    static string? BootedCheckpoint()
    {
        try { return Checkpoints.Checkpoints.Booted(File.ReadAllText("/proc/cmdline")); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>What the boot service wrote about this start (null: none).</summary>
    static JsonElement? RunJson(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.Clone();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
    }

    static string S(JsonElement? e, string k) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    static int CheckpointStatus(bool json)
    {
        var booted = BootedCheckpoint();
        var run = booted != null ? RunJson(Checkpoints.Checkpoints.RunState) : null;
        var restored = booted == null ? RunJson(Checkpoints.Checkpoints.RunRestored) : null;
        var menu = RunJson(Checkpoints.Checkpoints.MenuState);
        var inMenu = menu is { } m && m.TryGetProperty("entries", out var es) && es.ValueKind == JsonValueKind.Array ? es.GetArrayLength() : 0;
        var supported = Checkpoints.Checkpoints.MainSubvol() != null;
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "supported", supported },
                { "booted", booted == null ? null : new Fields
                    {
                        { "id", booted },
                        { "when", S(run, "when") },
                        { "kind", S(run, "kind") is { Length: > 0 } k ? k : "other" },
                        { "title", S(run, "title") is { Length: > 0 } t ? t : "a checkpoint" },
                        { "main", S(run, "main") },
                    } },
                { "restored", restored == null ? null : new Fields { { "from", S(restored, "from") }, { "previous", S(restored, "previous") }, { "until", S(restored, "until") } } },
                { "in_menu", inMenu },
            }));
            return 0;
        }
        if (booted != null)
        {
            var when = DateTimeOffset.TryParse(S(run, "when"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var w) ? w.ToLocalTime().ToString("MMM d, HH:mm", CultureInfo.InvariantCulture) : "";
            Console.WriteLine($"{Style.Amber}You're on a checkpoint{(when != "" ? " from " + when : "")}: {(S(run, "title") is { Length: > 0 } t ? t : "a checkpoint")}.{Style.Reset}");
            Console.WriteLine("The system is as it was then; what you change here is lost at the next start.");
            Console.WriteLine("  keep it as the main system:   sudo mazapan checkpoint keep");
            Console.WriteLine("  back to the main system:      restart");
            Console.WriteLine("  what changed (for an agent):  mazapan checkpoint diagnose");
            return 0;
        }
        Console.WriteLine("On the main system.");
        if (restored != null) Console.WriteLine($"It was checkpoint {S(restored, "from")}; the system it replaced is kept as {S(restored, "previous")} until {S(restored, "until")}.");
        Console.WriteLine(!supported ? "Checkpoints need Mazapán's layout (the system in a btrfs subvolume of its own)." :
            inMenu > 0 ? $"{inMenu} checkpoint{(inMenu == 1 ? "" : "s")} in the boot menu (Checkpoints)." : "No checkpoints in the boot menu yet: one is taken before every package change.");
        return 0;
    }

    static int CheckpointList(bool json)
    {
        var dir = BootedCheckpoint() != null ? Path.Join(Checkpoints.Checkpoints.MainMount, ".snapshots") : "/.snapshots";
        var list = Checkpoints.Checkpoints.List(dir);
        var menu = RunJson(Checkpoints.Checkpoints.MenuState);
        var inMenu = menu is { } m && m.TryGetProperty("entries", out var es) && es.ValueKind == JsonValueKind.Array
            ? es.EnumerateArray().Select(e => S(e, "id")).ToHashSet() : [];
        if (json)
        {
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "checkpoints", list.Select(c => (object?)new Fields
                    {
                        { "id", c.Id },
                        { "when", c.When.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
                        { "kind", c.Kind },
                        { "packages", c.Packages },
                        { "title", c.Title },
                        { "in_menu", inMenu.Contains(c.Id) },
                    }).ToList() },
            }));
            return 0;
        }
        if (list.Count == 0)
        {
            Console.WriteLine("No checkpoints yet: one is taken before every package change.");
            return 0;
        }
        foreach (var c in list)
            Console.WriteLine($"{c.Id,5}  {c.When.ToLocalTime().ToString("MMM d HH:mm", CultureInfo.InvariantCulture)}  {c.Title}{(inMenu.Contains(c.Id) ? $"  {Style.Dim}(in the boot menu){Style.Reset}" : "")}");
        return 0;
    }

    static int CheckpointKeep(string? id, int entries, int days, bool yes)
    {
        var booted = BootedCheckpoint();
        id ??= booted ?? throw new MazapanException("not on a checkpoint: to make one the main system, mazapan checkpoint restore N");
        NeedRoot(booted != null ? "keep" : "restore");
        if (!yes && !Console.IsInputRedirected)
        {
            Console.WriteLine(booted == id
                ? $"Checkpoint {id} becomes the main system. The one it replaces is kept {days} days as the previous system (in the boot menu), then deleted."
                : $"At the next start the system is checkpoint {id}. The one running now is kept {days} days as the previous system (in the boot menu), then deleted. Restart right after.");
            if (!Confirm("Go ahead?")) return 1;
        }
        Console.WriteLine(Checkpoints.Checkpoints.Keep(id, entries, days));
        Console.WriteLine($"{Style.Green}Done: restart to start on it.{Style.Reset}");
        return 0;
    }

    /// <summary>
    /// What changed between a checkpoint and the main system, as text for
    /// the person or an agent (read only): the packages that differ,
    /// Mazapán's last update, and the last starts' errors.
    /// </summary>
    static int CheckpointDiagnose(string? id)
    {
        var booted = BootedCheckpoint();
        var b = new StringBuilder();
        string a, bRoot, aName, bName;
        if (booted != null)
        {
            var run = RunJson(Checkpoints.Checkpoints.RunState);
            b.Append($"# Started from a checkpoint ({booted}): {S(run, "title")}, taken {S(run, "when")}\n\n");
            b.Append("This start is the system as it was then, on an overlay in memory (changes are lost at the next start). ");
            b.Append("The main system, the one that didn't work, is mounted read only at " + Checkpoints.Checkpoints.MainMount + ".\n");
            b.Append("To keep this checkpoint as the main system: `sudo mazapan checkpoint keep`. To go back: restart.\n\n");
            a = "/";
            bRoot = Checkpoints.Checkpoints.MainMount;
            aName = "checkpoint";
            bName = "main system";
        }
        else
        {
            var list = Checkpoints.Checkpoints.List();
            var c = id == null ? list.FirstOrDefault() : list.FirstOrDefault(x => x.Id == id);
            if (c == null) throw new MazapanException(id == null ? "no checkpoints yet" : $"checkpoint {id}: not there");
            b.Append($"# The running system and checkpoint {c.Id} ({c.Title}, taken {c.When.ToLocalTime():yyyy-MM-dd HH:mm})\n\n");
            b.Append($"To make the checkpoint the main system at the next start: `sudo mazapan checkpoint restore {c.Id}`.\n\n");
            a = $"/.snapshots/{c.Id}/snapshot";
            bRoot = "/";
            aName = "checkpoint";
            bName = "now";
        }

        var diff = Checkpoints.Checkpoints.Diff(Checkpoints.Checkpoints.Packages(a), Checkpoints.Checkpoints.Packages(bRoot));
        b.Append($"## Packages that differ ({diff.Count})\n\n");
        if (diff.Count == 0) b.Append("None (or the other system's package database couldn't be read).\n");
        foreach (var (name, x, y) in diff.Take(300))
            b.Append($"- {name}: {(x == "" ? "(not installed)" : x)} ({aName}) → {(y == "" ? "(not installed)" : y)} ({bName})\n");
        if (diff.Count > 300) b.Append($"- … and {diff.Count - 300} more\n");

        var last = History.List().FirstOrDefault();
        if (last != null)
        {
            b.Append($"\n## Mazapán's last update ({last.ID})\n\nOutcome: {last.Outcome}");
            if (last.Note != "") b.Append($"; {last.Note}");
            b.Append('\n');
            foreach (var r in Health.Checks.Failed(last.Checks)) b.Append($"- check failed: {r.Name}: {r.Output.Trim()}\n");
        }

        string Journal(params string[] args)
        {
            var r = Exec.Run("journalctl", args.Concat(["--no-pager", "-q"]));
            // Nothing found (-g exits 1 then, saying nothing): none.
            if (r.Stdout.Trim() == "") return r.Stderr.Trim() == "" ? "(none)" : $"(journalctl: {r.Stderr.Trim()})";
            return r.Stdout.TrimEnd();
        }
        b.Append("\n## The last starts\n\n```\n").Append(Journal("--list-boots")).Append("\n```\n");
        // On a checkpoint, the start before this one is the one that went wrong.
        var which = booted != null ? "-1" : "0";
        b.Append($"\n## Errors in {(booted != null ? "the start before this one" : "this start")}\n\n```\n");
        b.Append(Tail(Journal("-b", which, "-p", "3"), 80)).Append("\n```\n");
        b.Append($"\n## Services that failed in it\n\n```\n");
        b.Append(Tail(Journal("-b", which, "-g", "Failed to start|failed with result|core dumped"), 40)).Append("\n```\n");
        Console.Write(b.ToString());
        return 0;
    }
}
