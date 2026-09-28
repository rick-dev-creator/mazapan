using MyArch.Applying;
using MyArch.Rendering;

namespace MyArch.Tests.Applying;

/// <summary>A temporary directory, gone after the test (Go's t.TempDir).</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("myarch-test-").FullName;

    public string this[string name] => System.IO.Path.Join(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException) { }
    }
}

public sealed class ApplyTests : IDisposable
{
    readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    internal static RenderedFile File(string path, string content) => new() { Plugin = "p", Path = path, Content = content };

    internal static State PlanOne(RenderedFile f, Owned owned) => Apply.Plan([f], owned).Changes[0].State;

    internal static void Write(string path, string content) => System.IO.File.WriteAllText(path, content);

    internal static string ReadText(string path) => System.IO.File.ReadAllText(path);

    [Fact]
    public void PlanStates()
    {
        var p = dir["a.conf"];

        Assert.Equal(State.New, PlanOne(File(p, "v1"), new Owned()));

        Write(p, "v1");
        Assert.Equal(State.Unchanged, PlanOne(File(p, "v1"), new Owned()));
        Assert.Equal(State.Conflict, PlanOne(File(p, "v2"), new Owned()));
        Assert.Equal(State.Changed, PlanOne(File(p, "v2"), new Owned { [p] = Apply.Sum("v1") }));

        // We wrote v1, then a person edited it: that's theirs now.
        Write(p, "hand edit");
        Assert.Equal(State.Conflict, PlanOne(File(p, "v2"), new Owned { [p] = Apply.Sum("v1") }));
    }

    [Fact]
    public void ConflictNeedsAdopt()
    {
        var p = dir["a.conf"];
        Write(p, "mine");
        var owned = new Owned();

        var (ch, orphans) = Apply.Plan([File(p, "generated")], owned);
        var ce = Assert.Throws<ConflictException>(() => Apply.Execute(ch, orphans, owned, false));
        Assert.Equal([p], ce.Paths);
        Assert.Equal(
            "these files exist and were not written by myarch (or were edited since):\n  " + p +
            "\nre-run with --adopt to back them up and take them over", ce.Message);
        Assert.Equal("mine", ReadText(p)); // not touched without --adopt

        var res = Apply.Execute(ch, orphans, owned, true);
        Assert.Equal("mine", ReadText(res.Backups[p]));
        Assert.Equal("generated", ReadText(p));
    }

    [Fact]
    public void Orphans()
    {
        var clean = dir["clean.conf"];
        var edited = dir["edited.conf"];
        Write(clean, "gen");
        Write(edited, "hand edit");
        var owned = new Owned { [clean] = Apply.Sum("gen"), [edited] = Apply.Sum("gen") };

        var (ch, orphans) = Apply.Plan([], owned);
        var res = Apply.Execute(ch, orphans, owned, false);
        Assert.False(System.IO.File.Exists(clean)); // an untouched orphan is removed
        Assert.True(System.IO.File.Exists(edited)); // an edited orphan is left in place
        Assert.Single(res.Removed);
        Assert.Single(res.Kept);
        Assert.Empty(owned);
    }

    [Fact]
    public void OrphansAreSorted()
    {
        var owned = new Owned { ["/b"] = "x", ["/a"] = "y", ["/c"] = "z" };
        var (_, orphans) = Apply.Plan([File("/c", "")], owned);
        Assert.Equal(["/a", "/b"], orphans);
    }

    [Fact]
    public void StateNames()
    {
        Assert.Equal("unchanged new changed conflict busy unreadable",
            string.Join(" ", Enum.GetValues<State>().Select(s => s.Name())));
    }

    [Fact]
    public void Sum() =>
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", Apply.Sum(""));

    // owned.json as Go's json.MarshalIndent wrote it: 2 spaces, sorted keys,
    // HTML-safe, \u0000 for the shared keys' separators, a final newline.
    [Fact]
    public void OwnedJsonMatchesGo()
    {
        var path = dir["state/owned.json"];
        var owned = new Owned
        {
            ["/home/u/.config/qt6ct/qt6ct.conf"] = "ini:Appearance\0style\0Fusion\nFonts\0general\0\"Mono,11\"",
            ["/home/u/a&b<c>.conf"] = "abc",
            ["/home/u/.config/foot/foot.ini"] = "e3b0",
        };
        owned.Save(path);
        const string want = "{\n" +
            "  \"/home/u/.config/foot/foot.ini\": \"e3b0\",\n" +
            "  \"/home/u/.config/qt6ct/qt6ct.conf\": \"ini:Appearance\\u0000style\\u0000Fusion\\nFonts\\u0000general\\u0000\\\"Mono,11\\\"\",\n" +
            "  \"/home/u/a\\u0026b\\u003cc\\u003e.conf\": \"abc\"\n" +
            "}\n";
        Assert.Equal(want, ReadText(path));
        var back = Apply.LoadOwned(path);
        Assert.Equal(owned.OrderBy(kv => kv.Key), back.OrderBy(kv => kv.Key));

        new Owned().Save(path);
        Assert.Equal("{}\n", ReadText(path));
        Assert.Empty(Apply.LoadOwned(dir["missing.json"]));
    }

    [Fact]
    public void LoadOwnedErrorsAreGos()
    {
        var path = dir["owned.json"];
        Write(path, "{\"a\": 1}");
        Assert.Equal("json: cannot unmarshal number into Go value of type string",
            Assert.Throws<MyArch.Util.MyArchException>(() => Apply.LoadOwned(path)).Message);
        Write(path, "{\"a\": \"x\",}");
        Assert.Equal("invalid character '}' looking for beginning of object key string",
            Assert.Throws<MyArch.Util.MyArchException>(() => Apply.LoadOwned(path)).Message);
        Write(path, "{\"a\": ");
        Assert.Equal("unexpected end of JSON input",
            Assert.Throws<MyArch.Util.MyArchException>(() => Apply.LoadOwned(path)).Message);
        Write(path, "[]");
        Assert.Equal("json: cannot unmarshal array into Go value of type apply.Owned",
            Assert.Throws<MyArch.Util.MyArchException>(() => Apply.LoadOwned(path)).Message);
    }

    [Fact]
    public void ReloadRunsEachCommandOnce()
    {
        var log = dir["log"];
        var ok = new Change(new RenderedFile { Path = "/a", Reload = $"echo x >> '{log}'" });
        var twice = new Change(new RenderedFile { Path = "/b", Reload = ok.Reload });
        var bad = new Change(new RenderedFile { Path = "/c", Reload = "echo out; echo err >&2; exit 3" });
        var errs = Apply.Reload([ok, twice, bad, new Change(new RenderedFile { Path = "/d" })]);
        Assert.Equal("x\n", ReadText(log));
        Assert.Single(errs);
        var e = errs[bad.Reload];
        Assert.StartsWith("exit status 3: ", e);
        Assert.Contains("out", e);
        Assert.Contains("err", e);
    }

    [Fact]
    public void ReloadTimesOut()
    {
        var err = Apply.RunShell("echo started; sleep 20", TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(300));
        Assert.Equal("signal: killed: started", err);
    }

    [Fact]
    public void ReloadGivesUpOnOutputHeldOpen()
    {
        // A daemon started in the background keeps the output open.
        var err = Apply.RunShell("sleep 3 &", TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(300));
        Assert.Equal("exec: WaitDelay expired before I/O complete: ", err);
        Assert.Null(Apply.RunShell("true", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));
    }
}
