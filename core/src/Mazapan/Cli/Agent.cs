using System.Globalization;
using Mazapan.Applying;
using Mazapan.Config;
using Mazapan.Health;
using Mazapan.Install;
using Mazapan.Plugins;
using Mazapan.Updates;
using Mazapan.Util;
using Result = Mazapan.Health.Result;

namespace Mazapan.Cli;

/// <summary>
/// What an agent (or a person) needs to see and change the desktop safely:
/// the whole state as data (status --json, doctor --json), changes previewed
/// exactly (apply --dry-run --diff) and every apply undoable (undo). The
/// JSON is documented in docs/agent-api.md; "version" says which one.
/// </summary>
public static partial class Program
{
    const int StatusVersion = 1;

    static int CmdStatus(string[] args)
    {
        var fs = new Flags("status")
            .Bool("json", "the whole state as data, for agents and scripts")
            .Bool("checks", "run every plugin's checks too (they run commands)")
            .Parse(args);
        var problems = new List<string>();
        Settings? cfg = null;
        try
        {
            cfg = Settings.Load();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            problems.Add(e.Message);
        }

        // Plugins: every one found, loading or not, and where it comes from.
        var plugins = new List<Fields>();
        var broken = new List<Fields>();
        try
        {
            var c = Catalog.Load();
            foreach (var p in c.All)
            {
                var f = new Fields
                {
                    { "id", p.Id },
                    { "name", p.Meta.Name },
                    { "version", p.Meta.Version },
                    { "enabled", c.Cfg.IsOn(p) && Applies(p) },
                };
                var origin = c.Origin(p);
                f.Add("origin", origin.StartsWith("git ") ? "git" : origin);
                if (LockedEntry(c.Lock, p) is { } e)
                {
                    f.Add("source", e.Source);
                    f.Add("commit", e.Commit);
                }
                f.Add("description", p.Meta.Description);
                plugins.Add(f);
            }
            foreach (var (id, err) in c.Broken) broken.Add(new Fields { { "id", id }, { "error", err } });
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            problems.Add(e.Message);
        }

        // The generated files against the disk.
        Session? s = null;
        Fields? files = null;
        try
        {
            s = Load();
            var (changes, orphans, _) = s.Plan();
            files = new Fields
            {
                { "total", changes.Count },
                { "unchanged", changes.Count(ch => ch.State == State.Unchanged) },
                {
                    "changes", changes.Where(ch => ch.State != State.Unchanged)
                        .Select(ch => new Fields { { "path", ch.Path }, { "plugin", ch.Plugin }, { "state", ch.State.Name() } }).ToList()
                },
                { "orphans", orphans },
            };
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            problems.Add(e.Message);
        }

        var records = History.List();
        var last = records.FirstOrDefault();
        var snaps = Snapshots.List();
        List<Result>? checks = null;
        if (fs.IsSet("checks") && s != null) checks = s.Out.Checks.Select(Checks.RunOne).ToList();

        if (fs.IsSet("json"))
        {
            var theme = s == null ? null : new Fields
            {
                { "id", s.Theme.Id },
                { "name", s.Theme.Meta.Name },
                { "mode", s.Theme.Meta.Mode },
                { "accent", s.Theme.Colors.GetValueOrDefault("accent", "") },
                { "custom_accent", cfg?.Accent ?? "" },
            };
            var out_ = new Fields
            {
                { "version", StatusVersion },
                { "theme", theme },
                { "language", s?.Lang ?? "" },
                { "plugins", plugins },
                { "broken", broken },
                { "files", files },
                {
                    "updates", new Fields
                    {
                        { "last", last == null ? null : RecordJson(last) },
                        { "live", records.Where(r => r.Live()).Select(r => r.ID).ToList() },
                    }
                },
                {
                    "undo", snaps.Select(sn => new Fields
                    {
                        { "id", sn.ID },
                        { "time", sn.Time.ToString("o", CultureInfo.InvariantCulture) },
                        { "what", sn.What },
                        { "files", sn.Files.Count },
                        { "as_root", sn.Files.Any(e => AsRoot.IsSystem(e.Path)) || sn.Packages.Count > 0 },
                    }).ToList()
                },
            };
            if (checks != null) out_.Add("checks", ChecksJson(checks));
            if (cfg != null) out_.Add("hardware_to_offer", HardwareToOffer(cfg).Select(p => p.Id).ToList());
            out_.Add("problems", problems);
            Console.WriteLine(GoJson.Marshal(out_));
            return problems.Count > 0 ? 1 : 0;
        }

        if (s != null)
            Console.WriteLine($"theme {s.Theme.Id} ({s.Theme.Meta.Mode}), accent {s.Theme.Colors.GetValueOrDefault("accent")}, language {s.Lang}");
        Console.WriteLine($"{plugins.Count(p => (bool)p.First(kv => kv.Key == "enabled").Value!)} of {plugins.Count} plugins enabled" +
            (broken.Count > 0 ? $", {broken.Count} broken" : ""));
        if (files != null && s != null)
        {
            var (changes, orphans, _) = s.Plan();
            var drift = changes.Where(ch => ch.State != State.Unchanged).ToList();
            if (drift.Count + orphans.Count == 0) Console.WriteLine($"generated files: all {changes.Count} as mazapan wrote them");
            else
            {
                Console.WriteLine("generated files:");
                PrintPlan(changes, orphans, true);
            }
        }
        if (last != null) Console.WriteLine($"last update: {last.ID} {last.Outcome}");
        Console.WriteLine(snaps.Count > 0 ? $"undo: {snaps[0].What} ({snaps[0].ID}), {snaps.Count} in all" : "undo: nothing to undo");
        if (checks != null)
            foreach (var r in checks)
                Console.WriteLine($"  {(r.Skipped ? "–" : r.OK ? "✓" : "✗")} {r.Name} ({r.Plugin})");
        foreach (var p in problems) Console.WriteLine($"{Style.Amber}{p}{Style.Reset}");
        return problems.Count > 0 ? 1 : 0;
    }

    static Fields RecordJson(Record r) => new()
    {
        { "id", r.ID },
        { "outcome", r.Outcome },
        { "finished", r.Finished == default ? "" : r.Finished.ToString("o", CultureInfo.InvariantCulture) },
        { "packages", r.Changes.Count },
        { "failed_checks", Checks.Failed(r.Checks).Count },
        { "note", r.Note },
    };

    static List<Fields> ChecksJson(IEnumerable<Result> results) => results.Select(r => new Fields
    {
        { "plugin", r.Plugin },
        { "name", r.Name },
        { "ok", r.OK },
        { "skipped", r.Skipped },
        { "output", r.Output },
        { "took_ms", (long)r.Took.TotalMilliseconds },
    }).ToList();

    static int CmdDoctor(string[] args)
    {
        var fs = new Flags("doctor").Bool("json", "the results as data").Parse(args);
        var s = Load();
        if (fs.IsSet("json"))
        {
            var results = s.Out.Checks.Select(Checks.RunOne).ToList();
            var failed = Checks.Failed(results).Count;
            Console.WriteLine(GoJson.Marshal(new Fields
            {
                { "version", StatusVersion },
                { "session", Checks.InSession() },
                { "checks", ChecksJson(results) },
                { "failed", failed },
            }));
            return failed > 0 ? 1 : 0;
        }
        var bad = Checks.Failed(RunChecks(s, "Checks"));
        foreach (var p in HardwareToOffer(s.Cfg))
            Console.WriteLine($"\n{Style.Amber}○{Style.Reset} {p.Id} is for this machine ({p.Meta.Description}): mazapan hardware");
        if (bad.Count > 0) throw new MazapanException($"{bad.Count} checks failed");
        return 0;
    }

    static int CmdUndo(string[] args)
    {
        var fs = new Flags("undo")
            .Bool("list", "what can be undone, newest first")
            .Bool("y", "don't ask")
            .String("id", "undo this apply only if it's still the last one")
            .Bool("agent", "what an agent may undo: nothing done as root (the MCP server's undo)")
            .Parse(args);
        var snaps = Snapshots.List();
        if (fs.IsSet("list"))
        {
            if (snaps.Count == 0) Console.WriteLine("Nothing to undo.");
            foreach (var sn in snaps)
                Console.WriteLine($"{sn.ID}  {sn.Time.LocalDateTime:yyyy-MM-dd HH:mm}  {Plural(sn.Files.Count, "file", "files"),-9} {sn.What}");
            return 0;
        }
        using var lk = ApplyLock.Take();
        snaps = Snapshots.List();
        if (snaps.Count == 0) throw new MazapanException("nothing to undo");
        var snap = snaps[0];
        // An agent undoes its own change, not one the person made after it.
        if (fs.Get("id") is { Length: > 0 } want && want != snap.ID)
            throw new MazapanException(snaps.Any(x => x.ID == want)
                ? $"{want} isn't the last apply any more ({snap.ID}: {snap.What} came after it): undo that first, or leave it"
                : $"no apply {want} to undo (mazapan undo --list)");
        // An agent undoes only what it did itself (the person's change, or another
        // agent's, is the person's to undo: from History).
        var me = AgentClient != "" ? AgentClient : "an agent";
        if (fs.IsSet("agent") && snap.By != me)
            throw new MazapanException(snap.By == ""
                ? $"{snap.ID} ({snap.What}) was the person's change: undoing it is theirs (History, or mazapan undo)"
                : $"{snap.ID} ({snap.What}) was {snap.By}'s change, not this agent's: undoing it is the person's");
        var asRoot = snap.Files.Any(e => AsRoot.IsSystem(e.Path)) || snap.Packages.Count > 0;
        if (asRoot && fs.IsSet("agent"))
            throw new MazapanException($"{snap.ID} ({snap.What}) was done as root: undoing it is the person's (mazapan undo)");
        Header($"Undo {snap.What} ({snap.ID})");
        foreach (var e in snap.Files)
            Console.WriteLine($"  {(e.Before == "" ? "remove " : "restore"),-8} {Tilde(e.Path)}");
        foreach (var pkg in snap.Packages) Console.WriteLine($"  {"uninstall",-8} {pkg}");
        // What goes back as root comes from the person's own files (the
        // snapshot, the remembered reloads), which any program of theirs could
        // have changed: in full, before sudo, as apply --system shows its own.
        var rootReloads = SystemState.Reloads();
        var system = snap.Files.Where(e => AsRoot.IsSystem(e.Path)).ToList();
        if (system.Count > 0)
        {
            Header("As root, with sudo");
            foreach (var e in system)
            {
                var now = Applying.Apply.ReadOrNull(e.Path) is { } b ? Files.Utf8.GetString(b) : "";
                var back = e.Before == "" ? "" : File.ReadAllText(Paths.Join(snap.Dir(), e.Before));
                Console.Write(Diff.Unified(now, back, now == "" ? "/dev/null" : e.Path, back == "" ? "/dev/null" : e.Path));
            }
            foreach (var cmd in system.Select(e => rootReloads.GetValueOrDefault(e.Path, "")).Where(c => c != "").Distinct())
                Console.WriteLine($"  run      {cmd}");
        }
        if (!fs.IsSet("y"))
        {
            if (!IsTerminal(0)) throw new MazapanException("no terminal to ask on: undo with -y");
            if (!Confirm(system.Count > 0 ? "Put these back as they were (as root, as shown)?" : "Put these back as they were?")) return 0;
        }
        var res = Snapshots.Undo(snap, Settings.Path);
        // System files' reloads, as root, as when they were written: the
        // plugin may be off by now and not say them any more.
        foreach (var cmd in res.Restored.Concat(res.Removed).Where(AsRoot.IsSystem)
                     .Select(p => rootReloads.GetValueOrDefault(p, "")).Where(c => c != "").Distinct())
            if (AsRoot.Run(cmd) is { } err) Console.Error.WriteLine($"warning: reload {GoFormat.Quote(cmd)} (as root) failed: {err}");
        foreach (var p in res.Removed.Where(AsRoot.IsSystem)) rootReloads.Remove(p);
        SystemState.Save(rootReloads);
        foreach (var p in res.Later)
            Console.WriteLine($"  {Style.Amber}{Tilde(p)} changed since: left as it is{Style.Reset}");
        // Let what reads those files pick them up again.
        try
        {
            var s = Load();
            var touched = res.Restored.Concat(res.Removed).ToHashSet();
            var files = s.Out.Files.Where(f => touched.Contains(f.Path)).ToList();
            foreach (var (cmd, e) in Apply.Reload(files.Where(f => !f.System).Select(f => new Change(f))))
                Console.Error.WriteLine($"warning: reload {GoFormat.Quote(cmd)} failed: {e}");
        }
        catch (MazapanException e)
        {
            Console.Error.WriteLine($"warning: {e.Message}");
        }
        Console.WriteLine($"{Plural(res.Restored.Count, "file", "files")} back as they were" +
            (res.Removed.Count > 0 ? $", {res.Removed.Count} removed" : "") +
            (snap.Packages.Count > res.PackagesKept.Count ? ", packages it installed uninstalled" : "") + ".");
        foreach (var k in res.PackagesKept) Console.WriteLine($"  {Style.Dim}kept {k}{Style.Reset}");
        return 0;
    }
}
