using MyArch.Applying;
using MyArch.Rendering;
using MyArch.Tests.Applying;
using MyArch.Updates;
using Record = MyArch.Updates.Record;

namespace MyArch.Tests.Updates;

/// <summary>
/// Tests that point HOME somewhere else: they share the "HOME" collection,
/// so they never run at the same time as each other.
/// </summary>
[CollectionDefinition("HOME", DisableParallelization = true)]
public sealed class HomeCollection;

[Collection("HOME")]
public sealed class HistoryTests : IDisposable
{
    readonly TempDir home = new();
    readonly TempDir dir = new();
    readonly string? oldHome = Environment.GetEnvironmentVariable("HOME");

    public HistoryTests() => Environment.SetEnvironmentVariable("HOME", home.Path);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", oldHome);
        home.Dispose();
        dir.Dispose();
    }

    static void Write(string path, string content) => File.WriteAllText(path, content);

    static string ReadText(string path) => File.ReadAllText(path);

    /// <summary>
    /// SharedOwned writes content as a shared file (merge = "ini") through
    /// apply, and returns what Owned then has for it.
    /// </summary>
    static string SharedOwned(string path, string content)
    {
        var owned = new Owned();
        var f = new RenderedFile { Plugin = "p", Path = path, Content = content, Merge = "ini" };
        var (ch, orphans) = Apply.Plan([f], owned);
        Apply.Execute(ch, orphans, owned, true);
        return owned[path];
    }

    // A person's kdeglobals, taken over (shared) by an update: rolling back
    // must not delete it.
    [Fact]
    public void RestoreNeverRemovesASharedFile()
    {
        var p = dir["kdeglobals"];
        Write(p, "[KFileDialog Settings]\nx=1\n");
        var r = Record.New();
        r.BackupFiles(new Owned()); // it wasn't myarch's before the update
        var current = new Owned { [p] = SharedOwned(p, "[KDE]\nwidgetStyle=Fusion\n") };
        var res = r.RestoreFiles(current);
        Assert.True(File.Exists(p));
        Assert.Single(res.Shared);
        Assert.False(res.Owned.ContainsKey(p)); // kept and released
    }

    // Rolling back a shared file puts myarch's keys back and keeps what the
    // app wrote since.
    [Fact]
    public void RestoreSharedKeepsTheAppsKeys()
    {
        var p = dir["qt6ct.conf"];
        var before = SharedOwned(p, "[Appearance]\nstyle=Fusion\n");
        var r = Record.New();
        r.BackupFiles(new Owned { [p] = before });
        var after = SharedOwned(p, "[Appearance]\nstyle=Windows\n");
        // The app saves its window since.
        File.AppendAllText(p, "[SettingsWindow]\ngeometry=x\n");
        var res = r.RestoreFiles(new Owned { [p] = after });
        var got = ReadText(p);
        Assert.Contains("style=Fusion", got);
        Assert.Contains("geometry=x", got);
        Assert.Empty(res.Backups);
    }

    [Fact]
    public void BackupAndRestore()
    {
        var kept = dir["kept.conf"];
        var added = dir["added.conf"];
        Write(kept, "before");

        var r = Record.New();
        r.BackupFiles(new Owned { [kept] = Apply.Sum("before") });
        // The backup is private, under the record's files/ by absolute path.
        var copy = Path.Join(r.Dir(), "files", kept);
        Assert.Equal("before", ReadText(copy));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(copy));

        // The update rewrites one file and adds another.
        Write(kept, "after");
        Write(added, "new");
        var res = r.RestoreFiles(new Owned { [kept] = Apply.Sum("after"), [added] = Apply.Sum("new") });
        Assert.Equal("before", ReadText(kept));
        Assert.Equal(1, res.Files);
        Assert.Empty(res.Backups);
        Assert.False(File.Exists(added)); // a file the update added is removed
        Assert.Single(res.Owned);
        Assert.Equal(Apply.Sum("before"), res.Owned[kept]);
    }

    [Fact]
    public void RestoreKeepsHandEdits()
    {
        var kept = dir["kept.conf"];
        var added = dir["added.conf"];
        Write(kept, "before");
        var r = Record.New();
        r.BackupFiles(new Owned { [kept] = Apply.Sum("before") });

        // After the update, the person edited both files by hand.
        Write(kept, "my edit");
        Write(added, "my other edit");
        var res = r.RestoreFiles(new Owned { [kept] = Apply.Sum("after"), [added] = Apply.Sum("new") });
        Assert.Equal("my edit", ReadText(res.Backups[kept])); // the hand edit is kept in a backup
        Assert.Equal("my other edit", ReadText(added)); // an edited added file stays
        Assert.Single(res.LeftBehind);
    }

    [Fact]
    public void Before()
    {
        var r = Record.New();
        r.SaveBefore(new Dictionary<string, string> { ["foot"] = "1", ["a&b"] = "2" });
        Assert.Equal("{\"a\\u0026b\":\"2\",\"foot\":\"1\"}", ReadText(Path.Join(r.Dir(), "before.json")));
        var m = r.LoadBefore();
        Assert.NotNull(m);
        Assert.Equal("1", m["foot"]);
    }

    [Fact]
    public void HistoryList()
    {
        var old = new Record { ID = "20260101-000000", Outcome = Outcomes.OK, Finished = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var bad = new Record { ID = "20260201-000000", Outcome = Outcomes.RolledBack };
        old.Save();
        bad.Save();
        var list = History.List();
        Assert.Equal(2, list.Count);
        Assert.Equal(bad.ID, list[0].ID);
        Assert.Equal(old.Finished, History.LastSuccess()); // the last update that went through
    }

    [Fact]
    public void RestoreWithoutBackupTouchesNothing()
    {
        var f = dir["a.conf"];
        Write(f, "current");
        var r = Record.New(); // never backed up
        var res = r.RestoreFiles(new Owned { [f] = Apply.Sum("current") });
        Assert.Equal("current", ReadText(f));
        Assert.True(res.NoBackup);
        Assert.Single(res.Owned);
    }

    [Fact]
    public void EmptyBackupSurvivesSaving()
    {
        var r = Record.New();
        r.BackupFiles(new Owned()); // nothing generated yet: an empty backup, not none
        r.Outcome = Outcomes.OK;
        r.Save();
        Assert.NotNull(History.List()[0].Owned);
    }

    [Fact]
    public void RestoreLeavesLaterAppliesAlone()
    {
        var f = dir["theme.conf"];
        Write(f, "old theme");
        var r = Record.New();
        r.BackupFiles(new Owned { [f] = Apply.Sum("old theme") });
        r.AfterOwned = new Owned { [f] = Apply.Sum("updated") };
        // After the update, `myarch apply --theme other` rewrote it.
        Write(f, "other theme");
        var res = r.RestoreFiles(new Owned { [f] = Apply.Sum("other theme") });
        Assert.Equal("other theme", ReadText(f));
        Assert.Single(res.Later);
    }

    [Fact]
    public void RestoreSameContentIsNotAnEdit()
    {
        var f = dir["a.conf"];
        Write(f, "hand edited before the update");
        var r = Record.New();
        r.BackupFiles(new Owned { [f] = "sum the update never matched" });
        var res = r.RestoreFiles(new Owned { [f] = "sum the update never matched" });
        Assert.Empty(res.Backups);
        Assert.Equal(0, res.Files);
    }

    [Fact]
    public void RestoreStoppedHalfWayCarriesWhatItDid()
    {
        var f = dir["a.conf"];
        Write(f, "x");
        var r = Record.New();
        r.Owned = new Owned { [f] = Apply.Sum("x") }; // no copy under files/
        var e = Assert.Throws<RestoreException>(() => r.RestoreFiles(new Owned { [f] = Apply.Sum("y") }));
        Assert.Equal(Apply.Sum("y"), e.Partial.Owned[f]);
    }

    [Fact]
    public void LiveAndWorkOutChanges()
    {
        var failed = new Record { Outcome = Outcomes.Failed, Owned = new Owned() };
        Assert.False(failed.Live()); // a failed upgrade that changed nothing is not live
        var done = Record.New();
        done.Outcome = Outcomes.OK; // config-only update: no package changes
        done.SaveBefore(new Dictionary<string, string> { ["glibc"] = "1" });
        done.WorkOutChanges(() => new Dictionary<string, string> { ["glibc"] = "2" });
        Assert.Empty(done.Changes); // a finished update never gets changes worked out from today's packages
        var cut = Record.New();
        cut.Outcome = Outcomes.InProgress;
        cut.SaveBefore(new Dictionary<string, string> { ["foot"] = "1", ["gone"] = "3" });
        cut.WorkOutChanges(() => new Dictionary<string, string> { ["foot"] = "2", ["new"] = "1" });
        Assert.Equal(["foot 1 2", "gone 3 ", "new  1"], cut.Changes.Select(c => $"{c.Name} {c.From} {c.To}"));
    }

    // record.json as Go's json.MarshalIndent writes the struct: fields in
    // order, omitempty where Go's tags say, times in RFC 3339 with
    // nanoseconds, took in nanoseconds, owned null when there's no backup.
    [Fact]
    public void RecordJsonMatchesGo()
    {
        var r = new Record
        {
            ID = "20260928-162031",
            Started = new DateTimeOffset(2026, 9, 28, 16, 20, 31, TimeSpan.FromHours(-6)).AddTicks(3751234),
            Changes = [new MyArch.Pacman.Change("foot", "1.0", "1.1"), new MyArch.Pacman.Change("new", To: "2")],
            Checks = [new MyArch.Health.Result { Plugin = "p", Name = "bar <runs>", OK = true, Took = TimeSpan.FromMilliseconds(1.5) },
                      new MyArch.Health.Result { Plugin = "q", Name = "n", Skipped = true, Output = "no session", Took = TimeSpan.Zero }],
            Outcome = Outcomes.InProgress,
        };
        const string want = """
            {
              "id": "20260928-162031",
              "started": "2026-09-28T16:20:31.3751234-06:00",
              "finished": "0001-01-01T00:00:00Z",
              "changes": [
                {
                  "name": "foot",
                  "from": "1.0",
                  "to": "1.1"
                },
                {
                  "name": "new",
                  "to": "2"
                }
              ],
              "checks": [
                {
                  "plugin": "p",
                  "name": "bar \u003cruns\u003e",
                  "ok": true,
                  "took": 1500000
                },
                {
                  "plugin": "q",
                  "name": "n",
                  "ok": false,
                  "skipped": true,
                  "output": "no session",
                  "took": 0
                }
              ],
              "outcome": "in-progress",
              "owned": null
            }
            """;
        Assert.Equal(want, r.ToJson());
        r.Changes.Clear();
        r.Checks.Clear();
        r.Note = "n";
        r.Owned = new Owned();
        r.AfterOwned = new Owned();
        Assert.Equal("""
            {
              "id": "20260928-162031",
              "started": "2026-09-28T16:20:31.3751234-06:00",
              "finished": "0001-01-01T00:00:00Z",
              "outcome": "in-progress",
              "note": "n",
              "owned": {}
            }
            """, r.ToJson());
    }

    // What the Go version wrote reads back: nanoseconds, "Z", keys in any
    // case, unknown keys ignored; a record Go couldn't read is skipped.
    [Fact]
    public void ReadsWhatGoWrote()
    {
        const string go = """
            {
              "id": "20260928-162031",
              "started": "2026-09-28T16:20:31.375123456-06:00",
              "finished": "2026-09-28T22:25:00Z",
              "changes": [{"name": "linux", "from": "6.1", "to": "6.2"}],
              "checks": [{"plugin": "p", "name": "n", "ok": true, "took": 1234567891}],
              "outcome": "ok",
              "Note": "hi",
              "extra": [1, 2],
              "owned": {"/a": "ini:s\u0000k\u0000v"},
              "after_owned": {"/a": "x"}
            }
            """;
        var r = Record.FromJson(go);
        Assert.NotNull(r);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 16, 20, 31, TimeSpan.FromHours(-6)).AddTicks(3751234), r.Started);
        Assert.Equal(TimeSpan.FromHours(-6), r.Started.Offset);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 22, 25, 0, TimeSpan.Zero), r.Finished);
        Assert.Equal("linux", r.Changes[0].Name);
        Assert.Equal("6.2", r.Changes[0].To);
        Assert.Equal(TimeSpan.FromTicks(12345678), r.Checks[0].Took);
        Assert.Equal("hi", r.Note);
        Assert.Equal("ini:s\0k\0v", r.Owned!["/a"]);
        Assert.Equal("x", r.AfterOwned!["/a"]);
        Assert.Contains("\"started\": \"2026-09-28T16:20:31.3751234-06:00\"", r.ToJson());

        Assert.Null(Record.FromJson("{\"id\": 1}"));
        Assert.Null(Record.FromJson("{\"started\": \"yesterday\"}"));
        Assert.Null(Record.FromJson("{\"checks\": [{\"took\": 1.5}]}"));
        Assert.Null(Record.FromJson("[]"));
        Assert.Null(Record.FromJson("{"));
        Assert.Null(Record.FromJson("{\"owned\": null}")!.Owned);
        Assert.Equal("", Record.FromJson("null")!.ID);
    }

    [Fact]
    public void ListSkipsWhatItCantRead()
    {
        var root = History.Root();
        Directory.CreateDirectory(Path.Join(root, "20260101-000000"));
        Write(Path.Join(root, "20260101-000000", "record.json"), "{\"id\": \"20260101-000000\", \"outcome\": \"ok\"}");
        Directory.CreateDirectory(Path.Join(root, "20260102-000000"));
        Write(Path.Join(root, "20260102-000000", "record.json"), "{half");
        Directory.CreateDirectory(Path.Join(root, "empty"));
        var list = History.List();
        Assert.Single(list);
        Assert.Equal(default, History.LastSuccess());
    }

    [Fact]
    public void NoHistory()
    {
        Assert.Empty(History.List());
        Assert.Equal(default, History.LastSuccess());
    }
}
