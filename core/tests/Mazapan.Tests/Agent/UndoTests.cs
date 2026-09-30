using Mazapan.Applying;
using Mazapan.Rendering;
using Mazapan.Tests.Foundation;
using Mazapan.Util;

namespace Mazapan.Tests.Agent;

public class DiffTests
{
    [Fact]
    public void UnifiedLikeDiffU()
    {
        var a = "one\ntwo\nthree\nfour\nfive\nsix\nseven\neight\n";
        var b = "one\ntwo\nTHREE\nfour\nfive\nsix\nseven\neight\nnine\n";
        // As GNU diff -u: changes 5 lines apart share one hunk.
        Assert.Equal("""
            --- a
            +++ b
            @@ -1,8 +1,9 @@
             one
             two
            -three
            +THREE
             four
             five
             six
             seven
             eight
            +nine

            """.Replace("\r", ""), Diff.Unified(a, b, "a", "b"));
    }

    [Fact]
    public void FarApartChangesAreTwoHunks()
    {
        var a = string.Concat(Enumerable.Range(1, 20).Select(i => $"{i}\n"));
        var b = a.Replace("2\n3\n", "2\nthree\n").Replace("19\n", "nineteen\n");
        var d = Diff.Unified(a, b, "a", "b");
        Assert.Contains("@@ -1,6 +1,6 @@", d);
        Assert.Contains("@@ -16,5 +16,5 @@", d);
    }

    [Fact]
    public void AChangeOnTheFirstLine() =>
        Assert.Equal("--- a\n+++ b\n@@ -1,2 +1,2 @@\n-x\n+y\n z\n", Diff.Unified("x\nz\n", "y\nz\n", "a", "b"));

    [Fact]
    public void NewFileAndSame()
    {
        Assert.Equal("--- /dev/null\n+++ x\n@@ -0,0 +1,2 @@\n+a\n+b\n", Diff.Unified("", "a\nb\n", "/dev/null", "x"));
        Assert.Equal("", Diff.Unified("a\n", "a\n", "x", "x"));
    }
}

/// <summary>Every apply can be undone, and undo never loses what came after.</summary>
[Collection("HOME")]
public sealed class UndoTests : IDisposable
{
    readonly TempDir home = new();
    readonly string? oldHome = Environment.GetEnvironmentVariable("HOME");

    public UndoTests() => Environment.SetEnvironmentVariable("HOME", home.Path);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HOME", oldHome);
        home.Dispose();
    }

    string Apply1(string path, string content, string config)
    {
        var f = new RenderedFile { Plugin = "p", Path = path, Content = content };
        var owned = Apply.LoadOwned(Apply.StatePath());
        var (changes, orphans) = Apply.Plan([f], owned);
        var snap = Snapshots.Begin(changes, orphans, false, Config.Settings.Path, "apply");
        Apply.Execute(changes, orphans, owned, false);
        owned.Save(Apply.StatePath());
        Files.WriteAtomic(Config.Settings.Path, config);
        if (snap != null) Snapshots.Finish(snap, Config.Settings.Path);
        return path;
    }

    [Fact]
    public void UndoPutsBackFilesOwnershipAndConfig()
    {
        var path = Path.Join(home.Path, ".config", "x.conf");
        Apply1(path, "one\n", "theme = \"a\"\n");
        Apply1(path, "two\n", "theme = \"b\"\n");
        Assert.Equal(2, Snapshots.List().Count);

        var res = Snapshots.Undo(Snapshots.List()[0], Config.Settings.Path);
        Assert.Equal("one\n", File.ReadAllText(path));
        Assert.Equal("theme = \"a\"\n", File.ReadAllText(Config.Settings.Path));
        Assert.Equal(Apply.Sum(Files.Utf8.GetBytes("one\n")), Apply.LoadOwned(Apply.StatePath()).Get(path));
        Assert.Single(res.Restored);

        // The first apply created the file: undoing it removes it.
        Snapshots.Undo(Snapshots.List()[0], Config.Settings.Path);
        Assert.False(File.Exists(path));
        Assert.Empty(Snapshots.List());
    }

    [Fact]
    public void UndoLeavesWhatChangedSince()
    {
        var path = Path.Join(home.Path, ".config", "x.conf");
        Apply1(path, "one\n", "theme = \"a\"\n");
        Apply1(path, "two\n", "theme = \"b\"\n");
        File.WriteAllText(path, "edited by hand\n");
        var res = Snapshots.Undo(Snapshots.List()[0], Config.Settings.Path);
        Assert.Equal("edited by hand\n", File.ReadAllText(path));
        Assert.Contains(path, res.Later);
    }

    [Fact]
    public void OnlyTheLast20AreKept()
    {
        var path = Path.Join(home.Path, ".config", "x.conf");
        for (var i = 0; i < 23; i++)
        {
            Apply1(path, $"{i}\n", "");
            Thread.Sleep(2); // ids are to the millisecond
        }
        Assert.Equal(Snapshots.Keep, Snapshots.List().Count);
    }
}
