using System.Globalization;
using System.Text.RegularExpressions;
using Mazapan.Applying;
using Mazapan.Util;

namespace Mazapan.Updates;

// History keeps the record of each system update: what changed, how the
// checks went, whether it was rolled back, and a copy of the generated
// files from before, so a rollback puts back exactly what was there.

/// <summary>An update's outcome, as record.json has it.</summary>
public static class Outcomes
{
    /// <summary>started; still running, or interrupted</summary>
    public const string InProgress = "in-progress";
    /// <summary>updated, every check passed</summary>
    public const string OK = "ok";
    /// <summary>a check failed, everything was put back</summary>
    public const string RolledBack = "rolled-back";
    /// <summary>a check failed, not everything could be put back</summary>
    public const string RollbackIncomplete = "rollback-incomplete";
    /// <summary>the upgrade itself failed</summary>
    public const string Failed = "failed";
}

/// <summary>
/// Record is one update: ~/.local/state/mazapan/updates/&lt;id&gt;/record.json,
/// with the generated files from before it under files/.
/// </summary>
public sealed class Record
{
    public string ID = "";
    /// <summary>default (0001-01-01T00:00:00Z) is Go's zero time: not set.</summary>
    public DateTimeOffset Started;
    public DateTimeOffset Finished;
    public List<Pacman.Change> Changes = [];
    public List<Health.Result> Checks = [];
    /// <summary>The checks before the update: what already failed then isn't its doing.</summary>
    public List<Health.Result> Baseline = [];
    public string Outcome = "";
    public string Note = "";
    /// <summary>The apps it updated besides packages: Flatpak apps, apps from their makers (their names).</summary>
    public List<string> Apps = [];
    /// <summary>
    /// Generated files before the update. Written even when null: an empty
    /// backup ({}) and no backup at all (null) mean different things.
    /// </summary>
    public Owned? Owned;
    /// <summary>
    /// Generated files right after the update's apply. A rollback leaves
    /// alone what changed after that (a later `mazapan apply`), instead of
    /// undoing it behind config.toml's back.
    /// </summary>
    public Owned? AfterOwned;

    public static Record New()
    {
        var now = DateTimeOffset.Now;
        return new Record { ID = now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture), Started = now };
    }

    public string Dir() => Paths.Join(History.Root(), ID);

    public void Save()
    {
        // Atomic: the record is rewritten many times during an update, and one
        // cut off half way would be unreadable and vanish from the history.
        Io(() => Files.WriteAtomic(Paths.Join(Dir(), "record.json"), ToJson() + "\n"));
    }

    /// <summary>record.json as json.MarshalIndent writes the Go struct (without the final newline).</summary>
    public string ToJson()
    {
        var f = new GoJsonCodec.Fields
        {
            { "id", ID },
            { "started", GoTime.Format(Started) },
            { "finished", GoTime.Format(Finished) },
        };
        if (Changes.Count > 0)
            f.Add("changes", Changes.Select(c =>
            {
                var x = new GoJsonCodec.Fields { { "name", c.Name } };
                if (c.From != "") x.Add("from", c.From);
                if (c.To != "") x.Add("to", c.To);
                return (object?)x;
            }).ToList());
        static object? Check(Health.Result c)
        {
            var x = new GoJsonCodec.Fields { { "plugin", c.Plugin }, { "name", c.Name }, { "ok", c.OK } };
            if (c.Skipped) x.Add("skipped", true);
            if (c.Output != "") x.Add("output", c.Output);
            x.Add("took", c.Took.Ticks * 100); // time.Duration: nanoseconds
            return x;
        }
        if (Checks.Count > 0) f.Add("checks", Checks.Select(Check).ToList());
        if (Baseline.Count > 0) f.Add("baseline", Baseline.Select(Check).ToList());
        f.Add("outcome", Outcome);
        if (Note != "") f.Add("note", Note);
        if (Apps.Count > 0) f.Add("apps", Apps.Select(a => (object?)a).ToList());
        f.Add("owned", Owned);
        if (AfterOwned is { Count: > 0 }) f.Add("after_owned", AfterOwned);
        return GoJsonCodec.Marshal(f, true, "  ");
    }

    /// <summary>
    /// FromJson reads record.json as json.Unmarshal into the Go struct does
    /// (keys matched regardless of case, unknown ones ignored); null when
    /// Go's would fail.
    /// </summary>
    public static Record? FromJson(string text)
    {
        try
        {
            var r = new Record();
            switch (GoJsonCodec.Parse(text))
            {
                case null:
                    return r;
                case GoJsonCodec.JsonObject o:
                    foreach (var (k, v) in o) r.Set(k, v);
                    return r;
                case var other:
                    throw GoJsonCodec.TypeError(other, "update.Record");
            }
        }
        catch (GoJsonCodec.JsonError)
        {
            return null;
        }
    }

    void Set(string key, object? v)
    {
        static bool Is(string key, string name) => string.Equals(key, name, StringComparison.OrdinalIgnoreCase);
        if (Is(key, "id")) ID = Str(v, ID);
        else if (Is(key, "started")) Started = Time(v, Started);
        else if (Is(key, "finished")) Finished = Time(v, Finished);
        else if (Is(key, "changes")) Changes = List(v, Changes, PackageChangeFrom);
        else if (Is(key, "checks")) Checks = List(v, Checks, CheckResultFrom);
        else if (Is(key, "baseline")) Baseline = List(v, Baseline, CheckResultFrom);
        else if (Is(key, "outcome")) Outcome = Str(v, Outcome);
        else if (Is(key, "note")) Note = Str(v, Note);
        else if (Is(key, "apps")) Apps = List(v, Apps, x => x as string ?? "").Where(a => a != "").ToList();
        else if (Is(key, "owned")) Owned = Applying.Owned.FromTree(v, "apply.Owned");
        else if (Is(key, "after_owned")) AfterOwned = Applying.Owned.FromTree(v, "apply.Owned");
    }

    // A JSON null leaves a Go field as it was.
    static string Str(object? v, string was) => v switch
    {
        null => was,
        string s => s,
        _ => throw GoJsonCodec.TypeError(v, "string"),
    };

    static bool Bool(object? v, bool was) => v switch
    {
        null => was,
        bool b => b,
        _ => throw GoJsonCodec.TypeError(v, "bool"),
    };

    static DateTimeOffset Time(object? v, DateTimeOffset was) => v switch
    {
        null => was,
        string s => GoTime.Parse(s),
        _ => throw new GoJsonCodec.JsonError("Time.UnmarshalJSON: input is not a JSON string"),
    };

    static List<T> List<T>(object? v, List<T> was, Func<object?, T> item) => v switch
    {
        null => [],
        List<object?> a => a.Select(item).ToList(),
        _ => throw GoJsonCodec.TypeError(v, "[]" + typeof(T).Name),
    };

    static IEnumerable<KeyValuePair<string, object?>> Members(object? v, string goType) => v switch
    {
        null => [],
        GoJsonCodec.JsonObject o => o,
        _ => throw GoJsonCodec.TypeError(v, goType),
    };

    static Pacman.Change PackageChangeFrom(object? v)
    {
        string name = "", from = "", to = "";
        foreach (var (k, x) in Members(v, "pacman.Change"))
        {
            if (k.Equals("name", StringComparison.OrdinalIgnoreCase)) name = Str(x, name);
            else if (k.Equals("from", StringComparison.OrdinalIgnoreCase)) from = Str(x, from);
            else if (k.Equals("to", StringComparison.OrdinalIgnoreCase)) to = Str(x, to);
        }
        return new Pacman.Change(name, from, to);
    }

    static Health.Result CheckResultFrom(object? v)
    {
        string plugin = "", name = "", output = "";
        bool ok = false, skipped = false;
        var took = TimeSpan.Zero;
        foreach (var (k, x) in Members(v, "health.Result"))
        {
            if (k.Equals("plugin", StringComparison.OrdinalIgnoreCase)) plugin = Str(x, plugin);
            else if (k.Equals("name", StringComparison.OrdinalIgnoreCase)) name = Str(x, name);
            else if (k.Equals("ok", StringComparison.OrdinalIgnoreCase)) ok = Bool(x, ok);
            else if (k.Equals("skipped", StringComparison.OrdinalIgnoreCase)) skipped = Bool(x, skipped);
            else if (k.Equals("output", StringComparison.OrdinalIgnoreCase)) output = Str(x, output);
            else if (k.Equals("took", StringComparison.OrdinalIgnoreCase))
                took = x switch
                {
                    null => took,
                    GoJsonCodec.JsonNumber n => TimeSpan.FromTicks(GoJsonCodec.Int(n.Literal, "time.Duration") / 100),
                    _ => throw GoJsonCodec.TypeError(x, "time.Duration"),
                };
        }
        return new Health.Result { Plugin = plugin, Name = name, OK = ok, Skipped = skipped, Output = output, Took = took };
    }

    /// <summary>BackupFiles copies every generated file as it is now.</summary>
    public void BackupFiles(Owned owned)
    {
        Owned = new Owned();
        Io(() =>
        {
            foreach (var (path, sum) in owned)
            {
                var b = Apply.ReadOrNull(path);
                if (b == null) continue;
                var dst = Paths.Join(Dir(), "files", path);
                Directory.CreateDirectory(Paths.Dir(dst), Dir0755);
                Apply.WriteFile(dst, b, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                Owned[path] = sum;
            }
        });
    }

    const UnixFileMode Dir0755 = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// SaveBefore keeps the full package list from before the update. If
    /// the update is interrupted before its changes are recorded, a rollback
    /// works them out by comparing this with what's installed then.
    /// </summary>
    public void SaveBefore(IReadOnlyDictionary<string, string> installed)
    {
        var json = GoJsonCodec.Marshal(installed.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)).ToList());
        Io(() => Files.WriteAtomic(Paths.Join(Dir(), "before.json"), json));
    }

    /// <summary>
    /// WorkOutChanges fills in the changes of an update interrupted before
    /// it could record them, by comparing the packages from before it with
    /// now (installed; one that throws leaves them as they are).
    /// </summary>
    public void WorkOutChanges(Func<IReadOnlyDictionary<string, string>> installed)
    {
        // Only an interrupted update: one that finished with no package
        // changes (config only) changed none, and diffing its old package list
        // with today's would "revert" every upgrade made since.
        if (Outcome != Outcomes.InProgress || Changes.Count > 0) return;
        if (LoadBefore() is not { } before) return;
        IReadOnlyDictionary<string, string> now;
        try
        {
            now = installed();
        }
        catch (Exception)
        {
            return;
        }
        Changes = Pacman.Packages.Diff(before, now);
    }

    /// <summary>
    /// Live reports whether the update's changes are still in place: not
    /// rolled back, and not an upgrade that failed without changing
    /// anything.
    /// </summary>
    public bool Live() => Outcome != Outcomes.RolledBack && !(Outcome == Outcomes.Failed && Changes.Count == 0);

    /// <summary>The package list from before the update (before.json); null when it can't be read.</summary>
    public Dictionary<string, string>? LoadBefore()
    {
        byte[] b;
        try
        {
            b = File.ReadAllBytes(Paths.Join(Dir(), "before.json"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
        try
        {
            var m = Applying.Owned.FromTree(GoJsonCodec.Parse(Files.Utf8.GetString(b)), "map[string]string");
            return m == null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(m, StringComparer.Ordinal);
        }
        catch (GoJsonCodec.JsonError)
        {
            return null;
        }
    }

    /// <summary>
    /// RestoreFiles puts the generated files back as they were before the
    /// update and removes the ones it added. It never loses work:
    ///   - a file edited by hand since is backed up before being restored;
    ///   - a file a later `mazapan apply` rewrote is left alone (it follows
    ///     the config as it is now);
    ///   - an added file that was edited stays where it is.
    ///
    /// Owned in the result always matches the disk, even when it stops half
    /// way: then a RestoreException carries the result so far.
    /// </summary>
    public Restored RestoreFiles(Owned current)
    {
        var res = new Restored { Owned = new Owned(current) };
        // No copy of the files (a damaged or foreign record): "nothing was
        // there before" would mean deleting every generated file. Touch nothing.
        if (Owned == null)
        {
            res.NoBackup = true;
            return res;
        }
        try
        {
            Restore(res, current, Owned);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new RestoreException(res, e.Message, e);
        }
        catch (MazapanException e)
        {
            throw new RestoreException(res, e.Message, e);
        }
        return res;
    }

    void Restore(Restored res, Owned current, Owned before)
    {
        bool ChangedLater(string path) => AfterOwned != null && current.Get(path) != AfterOwned.Get(path);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        foreach (var (path, sum) in before)
        {
            if (ChangedLater(path))
            {
                res.Later.Add(path);
                continue;
            }
            var want = File.ReadAllBytes(Paths.Join(Dir(), "files", path));
            byte[]? disk = null;
            var missing = false;
            try
            {
                disk = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                missing = true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Read, but not readable: written over below, as Go does.
            }
            // Its directory is gone (a browser profile that was deleted): not
            // brought back to life to hold one file.
            if (missing)
            {
                var dir = Paths.Dir(path);
                if (!Paths.StatExists(dir))
                {
                    res.Gone.Add(path);
                    res.Owned.Remove(path);
                    continue;
                }
            }
            // A file shared with its app (kdeglobals, qt6ct.conf): only mazapan's
            // keys go back; what the app wrote since stays.
            if (disk != null && (Apply.IsShared(sum) || Apply.IsShared(current.Get(path))))
            {
                var merged = Apply.RestoreShared(disk, sum, current.Get(path), want);
                if (!merged.AsSpan().SequenceEqual(disk))
                {
                    if (Apply.Edited(current.Get(path), disk))
                    {
                        var bak = path + ".mazapan-bak-" + stamp;
                        Apply.WriteFile(bak, disk, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                        res.Backups[path] = bak;
                    }
                    Apply.WriteShared(path, merged);
                    res.Files++;
                }
                res.Owned[path] = sum;
                continue;
            }
            if (disk != null && disk.AsSpan().SequenceEqual(want))
            {
                // Already as it was: nothing to write, nothing to back up.
            }
            else
            {
                if (disk != null && Apply.Edited(current.Get(path), disk))
                {
                    var bak = path + ".mazapan-bak-" + stamp;
                    File.Move(path, bak, overwrite: true);
                    res.Backups[path] = bak;
                }
                Files.WriteAtomic(path, want);
                res.Files++;
            }
            res.Owned[path] = sum;
        }
        foreach (var path in current.Keys)
        {
            if (before.ContainsKey(path)) continue;
            if (ChangedLater(path))
            {
                res.Later.Add(path);
                continue;
            }
            // Shared with its app: it may well have been there before mazapan
            // took some of its keys. Never removed; just no longer managed.
            if (Apply.IsShared(current.Get(path)))
            {
                res.Shared.Add(path);
                res.Owned.Remove(path);
                continue;
            }
            byte[]? b = null;
            try
            {
                b = File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            if (b != null && Apply.Edited(current.Get(path), b))
            {
                res.LeftBehind.Add(path);
                continue;
            }
            File.Delete(path); // one that's gone already is fine
            res.Owned.Remove(path);
        }
    }

    static void Io(Action a)
    {
        try
        {
            a();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MazapanException(e.Message, e);
        }
    }
}

public static class History
{
    public static string Root() => Paths.ExpandHome("~/.local/state/mazapan/updates");

    /// <summary>List returns every record, newest first.</summary>
    public static List<Record> List()
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(Root());
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new MazapanException(e.Message, e);
        }
        var names = entries.Select(Paths.Base).ToList();
        GoOrder.Sort(names);
        var out_ = new List<Record>();
        foreach (var name in names)
        {
            byte[] b;
            try
            {
                b = File.ReadAllBytes(Paths.Join(Root(), name, "record.json"));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            if (Record.FromJson(Files.Utf8.GetString(b)) is { } r) out_.Add(r);
        }
        return out_.OrderByDescending(r => r.ID, GoOrder.Instance).ToList();
    }

    /// <summary>
    /// LastSuccess is when the system was last updated successfully, or the
    /// zero time (default).
    /// </summary>
    public static DateTimeOffset LastSuccess()
    {
        List<Record> records;
        try
        {
            records = List();
        }
        catch (MazapanException)
        {
            return default;
        }
        foreach (var r in records)
            if (r.Outcome == Outcomes.OK)
                return r.Finished;
        return default;
    }
}

/// <summary>Restored says what RestoreFiles did.</summary>
public sealed class Restored
{
    /// <summary>ownership matching the disk, even after an error</summary>
    public Owned Owned = new();
    /// <summary>files rewritten as they were</summary>
    public int Files;
    /// <summary>edited by hand since: path -> where the edit was kept</summary>
    public Dictionary<string, string> Backups = new(StringComparer.Ordinal);
    /// <summary>added since and edited by hand: left in place</summary>
    public List<string> LeftBehind = [];
    /// <summary>rewritten by a later `mazapan apply`: left as they are</summary>
    public List<string> Later = [];
    /// <summary>shared with their app, taken since: left in place, released</summary>
    public List<string> Shared = [];
    /// <summary>their directory is gone (a deleted profile): not restored</summary>
    public List<string> Gone = [];
    /// <summary>the record has no copy of the files: nothing touched</summary>
    public bool NoBackup;
}

/// <summary>
/// RestoreFiles stopped half way: Partial is what it did, and its Owned
/// matches the disk (save it).
/// </summary>
public sealed class RestoreException(Restored partial, string message, Exception inner) : Exception(message, inner)
{
    public Restored Partial { get; } = partial;
}

/// <summary>
/// Go's time.Time in JSON: RFC 3339 with up to nanoseconds, trailing zeros
/// dropped, "Z" for UTC. .NET keeps 100 ns: the last two digits of a time
/// Go wrote are lost when the record is written again.
/// </summary>
internal static partial class GoTime
{
    public static string Format(DateTimeOffset t)
    {
        var s = t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        var frac = t.Ticks % TimeSpan.TicksPerSecond;
        if (frac != 0) s += "." + frac.ToString("D7", CultureInfo.InvariantCulture).TrimEnd('0');
        if (t.Offset == TimeSpan.Zero) return s + "Z";
        var off = t.Offset;
        var sign = off < TimeSpan.Zero ? "-" : "+";
        off = off.Duration();
        return s + sign + off.Hours.ToString("D2", CultureInfo.InvariantCulture) + ":" + off.Minutes.ToString("D2", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^([0-9]{4})-([0-9]{2})-([0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?(?:(Z)|([+-])([0-9]{2}):([0-9]{2}))\z", RegexOptions.CultureInvariant)]
    private static partial Regex Rfc3339();

    /// <summary>Parses what Go's Time.UnmarshalJSON accepts (strict RFC 3339).</summary>
    public static DateTimeOffset Parse(string s)
    {
        var m = Rfc3339().Match(s);
        if (!m.Success) throw new GoJsonCodec.JsonError($"parsing time {GoFormat.Quote(s)} as \"2006-01-02T15:04:05Z07:00\": cannot parse");
        int N(int g) => int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture);
        try
        {
            var offset = TimeSpan.Zero;
            if (!m.Groups[8].Success)
            {
                if (N(10) >= 24 || N(11) >= 60) throw new ArgumentOutOfRangeException(nameof(s));
                offset = new TimeSpan(N(10), N(11), 0);
                if (m.Groups[9].Value == "-") offset = -offset;
            }
            var t = new DateTimeOffset(N(1), N(2), N(3), N(4), N(5), N(6), offset);
            if (m.Groups[7].Success)
            {
                var digits = m.Groups[7].Value;
                digits = digits.Length > 7 ? digits[..7] : digits.PadRight(7, '0');
                t = t.AddTicks(long.Parse(digits, CultureInfo.InvariantCulture));
            }
            return t;
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new GoJsonCodec.JsonError($"parsing time {GoFormat.Quote(s)}: out of range");
        }
    }
}
