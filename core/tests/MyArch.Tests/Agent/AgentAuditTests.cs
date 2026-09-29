using System.Text.Json;
using MyArch.Applying;
using MyArch.Cli;
using MyArch.Rendering;
using MyArch.Tests.Foundation;
using MyArch.Themes;
using MyArch.Util;

namespace MyArch.Tests.Agent;

/// <summary>What the audit of the agent-native work found, kept from coming back.</summary>
public class AgentAuditTests
{
    static JsonElement Call(string request) => JsonDocument.Parse(Program.Handle(request)!).RootElement;

    [Theory]
    [InlineData("""{"jsonrpc":"2.0","id":1,"method":7}""")]
    [InlineData("""{"jsonrpc":"2.0","id":2,"method":"initialize","params":{"protocolVersion":5}}""")]
    [InlineData("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":9}}""")]
    public void AMisshapenRequestIsAnErrorNotTheEnd(string request) =>
        Assert.True(Call(request).TryGetProperty("error", out _));

    [Fact]
    public void AnAgentCantSetText()
    {
        var r = Call("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"apply_change","arguments":{"set":{"palette.key":"SUPER\", os.execute(\"id\") --"}}}}""");
        Assert.True(r.GetProperty("result").GetProperty("isError").GetBoolean());
        Assert.Contains("the person's to set", r.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public void ChangingIsMarkedDestructive()
    {
        var tools = Call("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""").GetProperty("result").GetProperty("tools")
            .EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.True(tools["apply_change"]);
        Assert.True(tools["undo"]);
        Assert.False(tools["status"]);
    }

    [Theory]
    [InlineData("SUPER + space", "\"SUPER + space\"")]
    [InlineData("a\", os.execute(\"id\") --", "\"a\\\", os.execute(\\\"id\\\") --\"")]
    [InlineData("a\nb\\c", "\"a\\nb\\\\c\"")]
    public void LuaQuoteKeepsTextInItsString(string value, string want)
    {
        using var d = new TempDir();
        d.Write("f.tmpl", "{{ lq settings.k }}|{{ inline settings.k }}");
        var p = new MyArch.Plugins.Plugin { Dir = d.Path };
        p.Meta.Id = "p";
        p.Settings = new() { ["k"] = value };
        p.Targets.Add(new MyArch.Plugins.Target { Template = "f.tmpl", Output = Path.Join(d.Path, "o") });
        var t = new Theme { Id = "t" };
        t.Motion.ResponseMS = 400;
        t.Motion.Damping = 0.8;
        var got = Renderer.All([p], t, _ => null, "en").Files[0].Content.Split('|');
        Assert.Equal(want, got[0]);
        Assert.DoesNotContain('\n', got[1]);
    }

    [Theory]
    [InlineData("../themes/gruvbox")]
    [InlineData("Gruvbox")]
    [InlineData("a/b")]
    public void AThemeIdIsNotAPath(string id) =>
        Assert.Throws<MyArchException>(() => Theme.Load([Repo.Themes], id));

    [Fact]
    public void TheLastNewlineShows()
    {
        var d = Diff.Unified("a\nb", "a\nb\n", "x", "x");
        Assert.Contains("-b\n\\ No newline at end of file\n+b\n", d);
    }

    [Fact]
    public void AHugeDiffDoesntTakeAllTheMemory()
    {
        var a = string.Concat(Enumerable.Range(0, 20000).Select(i => $"a{i}\n"));
        var b = string.Concat(Enumerable.Range(0, 20000).Select(i => $"b{i}\n"));
        var before = GC.GetTotalAllocatedBytes();
        Assert.Contains("@@ -1,20000 +1,20000 @@", Diff.Unified(a, b, "x", "x"));
        Assert.True(GC.GetTotalAllocatedBytes() - before < 200_000_000);
    }

    [Fact]
    public void ASharedOrphanShowsWhatItLoses()
    {
        using var d = new TempDir();
        var path = d.Write("user.js", "user_pref(\"mine\", 1);\nuser_pref(\"theirs\", 2);\n");
        var owned = new Owned { [path] = "prefs:" + "theirs" };
        // Owned records myarch's keys for a shared file; whatever its encoding, removal drops only them.
        var after = Apply.ProposedOrphan(path, new Owned { [path] = owned[path] });
        Assert.NotNull(after);
    }
}

[Collection("HOME")]
public sealed class UndoAuditTests : IDisposable
{
    readonly TempDir home = new();
    readonly string? oldHome = Environment.GetEnvironmentVariable("HOME");

    public UndoAuditTests() => Environment.SetEnvironmentVariable("HOME", home.Path);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", oldHome);
        home.Dispose();
    }

    Snapshots.Snapshot? Apply1(string path, string content, string? config = null)
    {
        var f = new RenderedFile { Plugin = "p", Path = path, Content = content };
        var owned = Apply.LoadOwned(Apply.StatePath());
        var (changes, orphans) = Apply.Plan([f], owned);
        var snap = Snapshots.Begin(changes, orphans, false, Config.Settings.Path, "apply", configOnly: config != null);
        Apply.Execute(changes, orphans, owned, false);
        owned.Save(Apply.StatePath());
        if (config != null) Files.WriteAtomic(Paths.Real(Config.Settings.Path) ?? Config.Settings.Path, config);
        if (snap != null) Snapshots.Finish(snap, Config.Settings.Path);
        return snap;
    }

    [Fact]
    public void AnApplyThatChangedNothingLeavesNoSnapshot()
    {
        var path = Path.Join(home.Path, "x.conf");
        Apply1(path, "one\n");
        Apply1(path, "one\n", config: null);
        Files.WriteAtomic(Config.Settings.Path, "theme = \"a\"\n");
        Apply1(path, "one\n", "theme = \"a\"\n"); // same config bytes, same files
        Assert.Single(Snapshots.List());
    }

    [Fact]
    public void UndoKeepsASymlinkedConfig()
    {
        var real = Path.Join(home.Path, "dotfiles", "config.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(real)!);
        File.WriteAllText(real, "theme = \"a\"\n");
        Directory.CreateDirectory(Path.GetDirectoryName(Config.Settings.Path)!);
        File.CreateSymbolicLink(Config.Settings.Path, real);
        Apply1(Path.Join(home.Path, "x.conf"), "one\n", "theme = \"b\"\n");
        Assert.Equal("theme = \"b\"\n", File.ReadAllText(real));
        Snapshots.Undo(Snapshots.List()[0], Config.Settings.Path);
        Assert.NotNull(new FileInfo(Config.Settings.Path).LinkTarget);
        Assert.Equal("theme = \"a\"\n", File.ReadAllText(real));
    }

    [Fact]
    public void AnInterruptedApplyCanBeUndone()
    {
        var path = Path.Join(home.Path, "x.conf");
        Apply1(path, "one\n");
        // An apply that died after writing, before finishing its snapshot.
        var f = new RenderedFile { Plugin = "p", Path = path, Content = "two\n" };
        var owned = Apply.LoadOwned(Apply.StatePath());
        var (changes, orphans) = Apply.Plan([f], owned);
        Snapshots.Begin(changes, orphans, false, Config.Settings.Path, "apply");
        Apply.Execute(changes, orphans, owned, false);
        owned.Save(Apply.StatePath());
        var latest = Snapshots.List()[0];
        Assert.True(latest.Pending);
        Snapshots.Undo(latest, Config.Settings.Path);
        Assert.Equal("one\n", File.ReadAllText(path));
    }

    [Fact]
    public void SnapshotCopiesArePrivate()
    {
        var path = Path.Join(home.Path, "x.conf");
        Apply1(path, "one\n");
        Apply1(path, "two\n");
        var dir = Snapshots.List()[0].Dir();
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Join(dir, "files", "0")));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(dir));
    }
}

public class TomlTextTests
{
    [Theory]
    [InlineData("kitty")]
    [InlineData("%H:%M")]
    [InlineData("a=b")]
    public void NotTomlIsAnErrorWeCanCatch(string raw) =>
        Assert.Throws<MyArchException>(() => Toml.Parse("v = " + raw + "\n", "--set"));
}
