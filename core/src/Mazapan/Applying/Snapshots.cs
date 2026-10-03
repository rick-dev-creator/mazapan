using System.Globalization;
using System.Text.Json;
using Mazapan.Util;

namespace Mazapan.Applying;

/// <summary>
/// Snapshots make every apply reversible: before it writes, what it will
/// touch is copied (each file as it was, or that it wasn't there; owned.json;
/// config.toml, which --theme, --accent and --set change; and config.toml as
/// the apply left it, for the timeline to say what changed), and `mazapan
/// undo` puts the latest back. A file changed since (edited, or a later apply) is
/// left as it is: undo never loses what came after. The last 20 are kept.
///
/// The snapshot is written before the apply starts, marked pending, so an
/// apply killed half way can still be undone; the next apply or undo finishes
/// it with what's on disk. Copies are 0600 in 0700 folders: they can be a
/// browser's Preferences.
/// </summary>
public static class Snapshots
{
    public const int Keep = 20;

    const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    const UnixFileMode PrivateDir = Private | UnixFileMode.UserExecute;

    public static string Root() => Paths.ExpandHome("~/.local/state/mazapan/applies");

    public sealed class Entry
    {
        public string Path = "";
        /// <summary>Its copy in the snapshot, or "" when it wasn't there.</summary>
        public string Before = "";
        /// <summary>Its hash as it was ("" when it wasn't there).</summary>
        public string BeforeSum = "";
        /// <summary>What the apply left: its hash, "" when it removed it.</summary>
        public string After = "";
    }

    public sealed class Snapshot
    {
        public string ID = "";
        public DateTimeOffset Time;
        /// <summary>What it did, for listing: "theme gruvbox", "bar-clock.font_size".</summary>
        public string What = "";
        /// <summary>Who did it: an agent's name (through the MCP server), "" for the person.</summary>
        public string By = "";
        public List<Entry> Files = [];
        /// <summary>config.toml before and as the apply left it (hashes; "" when there was none).</summary>
        public string ConfigBefore = "", ConfigAfter = "";
        /// <summary>Written before the apply; finished after it. Pending: the apply didn't get to the end.</summary>
        public bool Pending;
        /// <summary>Packages the apply installed (apply --system): undo takes them out.</summary>
        public List<string> Packages = [];
        public string Dir() => Paths.Join(Root(), ID);
    }

    public static string SumOf(string path) => Apply.ReadOrNull(Paths.Real(path) ?? path) is { } b ? Apply.Sum(b) : "";

    /// <summary>
    /// Begin copies what the plan will touch: the files it writes or removes,
    /// owned.json and config.toml. Nothing to touch: null, unless configOnly
    /// (config.toml changes even though no file does). Call it holding the
    /// lock (Lock.Take).
    /// </summary>
    public static Snapshot? Begin(List<Change> changes, List<string> orphans, bool adopt, string configPath, string what, bool configOnly = false, string by = "")
    {
        Recover();
        var touched = changes
            .Where(c => c.State is State.New or State.Changed || (c.State == State.Conflict && adopt))
            .Select(c => c.Path).Concat(orphans).Distinct().ToList();
        if (touched.Count == 0 && !configOnly) return null;
        // UTC: a clock set back (daylight saving) can't make a newer one sort as older.
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var id = stamp;
        for (var n = 2; Directory.Exists(Paths.Join(Root(), id)); n++) id = stamp + "-" + n;
        var s = new Snapshot { ID = id, Time = DateTimeOffset.Now, What = what, By = by, Pending = true, ConfigBefore = SumOf(configPath) };
        var dir = s.Dir();
        try
        {
            Directory.CreateDirectory(Root(), PrivateDir);
            Directory.CreateDirectory(dir, PrivateDir);
            Directory.CreateDirectory(Paths.Join(dir, "files"), PrivateDir);
            for (var i = 0; i < touched.Count; i++)
            {
                var e = new Entry { Path = touched[i] };
                if (Apply.ReadOrNull(touched[i]) is { } b)
                {
                    e.Before = $"files/{i}";
                    e.BeforeSum = Apply.Sum(b);
                    WritePrivate(Paths.Join(dir, e.Before), b);
                }
                s.Files.Add(e);
            }
            if (Apply.ReadOrNull(Apply.StatePath()) is { } owned) WritePrivate(Paths.Join(dir, "owned.json"), owned);
            if (Apply.ReadOrNull(Paths.Real(configPath) ?? configPath) is { } config) WritePrivate(Paths.Join(dir, "config.toml"), config);
            Save(s);
        }
        catch (Exception)
        {
            Discard(s); // half a snapshot is none
            throw;
        }
        return s;
    }

    static void WritePrivate(string path, byte[] b)
    {
        using var f = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, UnixCreateMode = Private });
        f.Write(b);
    }

    /// <summary>Discard drops a snapshot whose apply didn't happen.</summary>
    public static void Discard(Snapshot? s)
    {
        if (s != null && Directory.Exists(s.Dir())) Directory.Delete(s.Dir(), true);
    }

    /// <summary>
    /// Finish records what the apply left on disk (config.toml included, so
    /// call it once that's saved), so undo can tell later changes apart, and
    /// keeps the last 20. One that changed nothing at all is dropped: undo
    /// must never "undo" nothing and leave the real change behind.
    /// </summary>
    public static void Finish(Snapshot s, string configPath)
    {
        foreach (var e in s.Files) e.After = SumOf(e.Path);
        s.ConfigAfter = SumOf(configPath);
        var after = Paths.Join(s.Dir(), "config.after.toml");
        if (s.ConfigAfter != s.ConfigBefore && Apply.ReadOrNull(Paths.Real(configPath) ?? configPath) is { } config && !File.Exists(after))
            WritePrivate(after, config);
        s.Pending = false;
        if (s.Files.All(e => e.After == e.BeforeSum) && s.ConfigAfter == s.ConfigBefore && s.Packages.Count == 0)
        {
            Discard(s);
            return;
        }
        Save(s);
        foreach (var old in List().Skip(Keep)) Directory.Delete(old.Dir(), true);
    }

    /// <summary>
    /// Recover finishes snapshots whose apply died half way (what's on disk is
    /// what it left), and drops folders that never became a snapshot.
    /// </summary>
    static void Recover()
    {
        if (!Directory.Exists(Root())) return;
        foreach (var dir in Directory.GetDirectories(Root()))
            if (!File.Exists(Paths.Join(dir, "snapshot.json")))
                Directory.Delete(dir, true);
        foreach (var s in List().Where(s => s.Pending))
        {
            s.What += " (interrupted)";
            foreach (var e in s.Files) e.After = SumOf(e.Path);
            s.ConfigAfter = s.ConfigBefore; // whether it got to config.toml is unknown: leave it
            s.Pending = false;
            Save(s);
        }
    }

    static void Save(Snapshot s)
    {
        var json = GoJson.Marshal(new Fields
        {
            { "id", s.ID },
            { "time", s.Time.ToString("o", CultureInfo.InvariantCulture) },
            { "what", s.What },
            { "by", s.By },
            { "pending", s.Pending },
            {
                "files", s.Files.Select(e => new Fields
                {
                    { "path", e.Path }, { "before", e.Before }, { "before_sum", e.BeforeSum }, { "after", e.After },
                }).ToList()
            },
            { "packages", s.Packages },
            { "config_before", s.ConfigBefore },
            { "config_after", s.ConfigAfter },
        });
        Files.WriteAtomic(Paths.Join(s.Dir(), "snapshot.json"), json + "\n");
    }

    /// <summary>
    /// ConfigFile is the snapshot's copy of config.toml before the apply, or
    /// after it (kept only when it changed); null when there's none.
    /// </summary>
    public static string? ConfigFile(Snapshot s, bool after)
    {
        var p = Paths.Join(s.Dir(), after ? "config.after.toml" : "config.toml");
        return File.Exists(p) ? p : null;
    }

    /// <summary>List is every snapshot, newest first; one that doesn't read is skipped.</summary>
    public static List<Snapshot> List()
    {
        var out_ = new List<Snapshot>();
        if (!Directory.Exists(Root())) return out_;
        foreach (var dir in Directory.GetDirectories(Root()))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(Paths.Join(dir, "snapshot.json")));
                var r = doc.RootElement;
                string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
                var s = new Snapshot
                {
                    ID = Str(r, "id"),
                    Time = DateTimeOffset.Parse(Str(r, "time"), CultureInfo.InvariantCulture),
                    What = Str(r, "what"),
                    By = Str(r, "by"),
                    Pending = r.TryGetProperty("pending", out var p) && p.ValueKind == JsonValueKind.True,
                    ConfigBefore = Str(r, "config_before"),
                    ConfigAfter = Str(r, "config_after"),
                };
                if (r.TryGetProperty("packages", out var pk))
                    foreach (var x in pk.EnumerateArray())
                        if (x.GetString() is { } name && AsRoot.IsPackage(name)) s.Packages.Add(name);
                foreach (var f in r.GetProperty("files").EnumerateArray())
                    s.Files.Add(new Entry { Path = Str(f, "path"), Before = Str(f, "before"), BeforeSum = Str(f, "before_sum"), After = Str(f, "after") });
                if (s.ID == Paths.Base(dir)) out_.Add(s);
            }
            catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException) { }
        }
        return out_.OrderByDescending(s => s.ID, StringComparer.Ordinal).ToList();
    }

    public sealed class Undone
    {
        public List<string> Restored = [];
        public List<string> Removed = [];
        /// <summary>Changed since the apply: left as they are.</summary>
        public List<string> Later = [];
        /// <summary>Packages the apply installed that stay: gone already, or needed by something since.</summary>
        public List<string> PackagesKept = [];
    }

    /// <summary>
    /// Undo puts the files of s back as they were, and owned.json and
    /// config.toml with them; a file changed since stays as it is (and
    /// ownership keeps what it has for it). What's done is recorded even when
    /// it stops half way, and a file already back as it was counts as done:
    /// undo again finishes it. The snapshot goes when it's all done. Call it
    /// holding the lock.
    /// </summary>
    public static Undone Undo(Snapshot s, string configPath)
    {
        Recover();
        s = List().FirstOrDefault(x => x.ID == s.ID) ?? s; // as Recover may have finished it
        var res = new Undone();
        var dir = s.Dir();
        var ownedBefore = File.Exists(Paths.Join(dir, "owned.json")) ? Apply.LoadOwned(Paths.Join(dir, "owned.json")) : new Owned();
        var owned = Apply.LoadOwned(Apply.StatePath());
        try
        {
            foreach (var e in s.Files)
            {
                var now = SumOf(e.Path);
                var already = now == e.BeforeSum;
                if (now != e.After && !already)
                {
                    res.Later.Add(e.Path);
                    continue;
                }
                var system = AsRoot.IsSystem(e.Path);
                if (e.Before == "")
                {
                    if (now != "")
                    {
                        if (system) AsRoot.Remove(e.Path);
                        else File.Delete(e.Path);
                    }
                    res.Removed.Add(e.Path);
                    owned.Remove(e.Path);
                    continue;
                }
                if (!already)
                {
                    var content = File.ReadAllBytes(Paths.Join(dir, e.Before));
                    // A shared file (kdeglobals) goes back through its symlink, as it was written.
                    if (system) AsRoot.Write(e.Path, content);
                    else if (Apply.IsShared(ownedBefore.Get(e.Path)) || Apply.IsShared(owned.Get(e.Path))) Apply.WriteShared(e.Path, content);
                    else Files.WriteAtomic(e.Path, content);
                }
                res.Restored.Add(e.Path);
                if (ownedBefore.TryGetValue(e.Path, out var sum)) owned[e.Path] = sum;
                else owned.Remove(e.Path);
            }
        }
        finally
        {
            // Ownership matches the disk, however far it got.
            owned.Save(Apply.StatePath());
        }
        // config.toml goes back too (through its symlink, as it's written),
        // unless it was changed since.
        var real = Paths.Real(configPath) ?? configPath;
        var configNow = SumOf(configPath);
        if (configNow != s.ConfigAfter && configNow != s.ConfigBefore) res.Later.Add(configPath);
        else if (configNow != s.ConfigBefore)
        {
            var copy = Paths.Join(dir, "config.toml");
            if (File.Exists(copy)) Files.WriteAtomic(real, File.ReadAllBytes(copy));
            else File.Delete(real);
        }
        // What it installed goes too, but what something else needs by now.
        res.PackagesKept = AsRoot.Uninstall(s.Packages);
        Directory.Delete(dir, true);
        return res;
    }
}

/// <summary>
/// One apply or undo at a time: the theme picker's, the palette's and an
/// agent's can come at once, and each must see the disk as the other left it.
/// </summary>
public sealed class ApplyLock : IDisposable
{
    readonly FileStream file;

    ApplyLock(FileStream f) => file = f;

    public void Dispose() => file.Dispose();

    /// <summary>Takes the lock, waiting for another apply to finish (a minute at most).</summary>
    public static ApplyLock Take()
    {
        var path = Paths.ExpandHome("~/.local/state/mazapan/lock");
        Files.CreateDirectory(Paths.Dir(path));
        var said = false;
        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        while (true)
        {
            try
            {
                // FileShare.None is an exclusive flock on Linux: gone with the process.
                return new ApplyLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                if (!said) Console.Error.WriteLine("waiting for another mazapan apply to finish…");
                said = true;
                Thread.Sleep(200);
            }
        }
    }
}
