using Mazapan.Install;
using Mazapan.Util;

namespace Mazapan.Tests.Install;

public class SourceTests
{
    // install_test.go: TestParseSource.
    [Theory]
    [InlineData("https://github.com/a/b", "https://github.com/a/b", "")]
    [InlineData("https://github.com/a/b#v1.2", "https://github.com/a/b", "v1.2")]
    [InlineData("git@github.com:a/b#feature/x", "git@github.com:a/b", "feature/x")]
    [InlineData("https://u@host/a/b", "https://u@host/a/b", "")]
    [InlineData("/home/me/my@plugins/x#abc1234", "/home/me/my@plugins/x", "abc1234")]
    public void ParseSource(string input, string source, string @ref)
    {
        var (s, r) = Git.ParseSource(input);
        Assert.Equal(source, s);
        Assert.Equal(@ref, r);
    }

    [Theory]
    [InlineData("--upload-pack=x")]
    [InlineData("u#--output=x")]
    [InlineData("u#a..b")]
    [InlineData("u#a b")]
    [InlineData("")]
    // Go's $ is the end of the text; .NET's would let a final newline through.
    [InlineData("u#v1\n")]
    public void ParseSourceRefuses(string bad) => Assert.Throws<MazapanException>(() => Git.ParseSource(bad));

    [Fact]
    public void Messages()
    {
        Assert.Equal("\"-x\" is not a git URL or folder",
            Assert.Throws<MazapanException>(() => Git.ParseSource("-x")).Message);
        Assert.Equal("\"a..b\" is not a branch, tag or commit",
            Assert.Throws<MazapanException>(() => Git.CheckRef("a..b")).Message);
        Git.CheckRef("");
        Git.CheckRef("release/1.x");
    }
}

[Collection("InstallHome")]
public class PluginsLockTests
{
    const string C1 = "1111111111111111111111111111111111111111";
    const string C2 = "2222222222222222222222222222222222222222222222222222222222222222";

    [Fact]
    public void NoFileIsEmpty()
    {
        using var sb = new Sandbox();
        Assert.Empty(PluginsLock.Load().Plugins);
        Assert.Equal(Path.Join(sb.Home, ".config/mazapan/plugins.lock"), PluginsLock.Path);
        Assert.Equal(Path.Join(sb.Home, ".local/share/mazapan/plugins"), Git.Dir);
    }

    [Fact]
    public void SaveWritesWhatGoWrote()
    {
        using var sb = new Sandbox();
        var l = new PluginsLock();
        l.Put(new Entry { Id = "zeta", Source = "https://x/zeta", Commit = C1, Approved = ["writes b", "runs a"] });
        l.Put(new Entry { Id = "alpha", Source = "/src/\"q\"", Ref = "v1", Commit = C2, Approved = [] });
        l.Save();
        Assert.Equal(
            "# Plugins installed from git: source, commit and what you approved.\n" +
            "# Written by mazapan plugins add/update/remove; with config.toml it\n" +
            "# reproduces this desktop elsewhere (mazapan plugins sync).\n\n" +
            "[[plugin]]\n" +
            "  id = \"alpha\"\n" +
            "  source = \"/src/\\\"q\\\"\"\n" +
            "  ref = \"v1\"\n" +
            "  commit = \"" + C2 + "\"\n" +
            "  approved = []\n" +
            "\n" +
            "[[plugin]]\n" +
            "  id = \"zeta\"\n" +
            "  source = \"https://x/zeta\"\n" +
            "  commit = \"" + C1 + "\"\n" +
            "  approved = [\"runs a\", \"writes b\"]\n",
            File.ReadAllText(PluginsLock.Path));
        var back = PluginsLock.Load();
        Assert.Equal(["alpha", "zeta"], back.Plugins.Select(e => e.Id));
        Assert.Equal("/src/\"q\"", back.Get("alpha")!.Source);
        Assert.Equal("v1", back.Get("alpha")!.Ref);
        Assert.Equal("", back.Get("zeta")!.Ref);
        Assert.Equal(["runs a", "writes b"], back.Get("zeta")!.Approved);
    }

    [Fact]
    public void EmptiedLockRoundTrips()
    {
        using var sb = new Sandbox();
        var l = new PluginsLock();
        l.Put(new Entry { Id = "a", Source = "s", Commit = C1 });
        l.Delete("a");
        l.Save();
        Assert.EndsWith("(mazapan plugins sync).\n\nplugin = []\n", File.ReadAllText(PluginsLock.Path));
        Assert.Empty(PluginsLock.Load().Plugins);
    }

    [Fact]
    public void FromACatalogIsKept()
    {
        using var sb = new Sandbox();
        var l = new PluginsLock();
        l.Put(new Entry { Id = "a", Source = "s", Ref = "v1.0", Commit = C1, Catalog = true });
        l.Put(new Entry { Id = "b", Source = "s", Commit = C1 });
        l.Save();
        Assert.Contains("  catalog = true\n", File.ReadAllText(PluginsLock.Path));
        var back = PluginsLock.Load();
        Assert.True(back.Get("a")!.Catalog);
        Assert.False(back.Get("b")!.Catalog);
        Assert.True(back.Get("a")!.Copy().Catalog);
    }

    [Fact]
    public void PutOverwritesInPlace()
    {
        var l = new PluginsLock();
        l.Put(new Entry { Id = "b", Source = "s", Commit = C1, Approved = ["y", "x"] });
        var e = l.Get("b")!;
        var next = e.Copy();
        next.Commit = C2;
        Assert.Equal(C1, e.Commit); // a copy
        l.Put(next);
        Assert.Equal(C2, e.Commit); // Go's pointer into the lock sees it
        Assert.Equal(["x", "y"], e.Approved);
        Assert.Single(l.Plugins);
        l.Put(new Entry { Id = "a", Source = "s", Commit = C1 });
        Assert.Equal(["a", "b"], l.Plugins.Select(p => p.Id));
        l.Delete("b");
        Assert.Null(l.Get("b"));
    }

    [Theory]
    [InlineData("id = \"a\"\nsource = \"s\"\ncommit = \"--upload-pack=x\"", "a: commit \"--upload-pack=x\" isn't a full commit id")]
    [InlineData("id = \"a\"\nsource = \"s\"\ncommit = \"1111111\"", "a: commit \"1111111\" isn't a full commit id")]
    [InlineData("id = \"a\"\nsource = \"s\"\ncommit = \"" + C1 + "\\n\"", "a: commit \"" + C1 + "\\n\" isn't a full commit id")]
    [InlineData("id = \"../x\"\nsource = \"s\"\ncommit = \"" + C1 + "\"", "id \"../x\": lowercase letters, digits and dashes")]
    [InlineData("id = \"a\\n\"\nsource = \"s\"\ncommit = \"" + C1 + "\"", "id \"a\\n\": lowercase letters, digits and dashes")]
    [InlineData("id = \"a\"\nsource = \"--upload-pack=x\"\ncommit = \"" + C1 + "\"", "a: source \"--upload-pack=x\"")]
    [InlineData("id = \"a\"\ncommit = \"" + C1 + "\"", "a: source \"\"")]
    [InlineData("id = \"a\"\nsource = \"s\"\nref = \"--output=x\"\ncommit = \"" + C1 + "\"", "a: \"--output=x\" is not a branch, tag or commit")]
    [InlineData("id = \"a\"\nsource = \"s\"\ncommit = \"" + C1 + "\"\nurl = \"x\"", "unknown keys: [plugin.url]")]
    public void LoadRefuses(string entry, string message)
    {
        using var sb = new Sandbox();
        Sandbox.Write(sb.Home, ".config/mazapan/plugins.lock", "[[plugin]]\n" + entry + "\n");
        var err = Assert.Throws<MazapanException>(PluginsLock.Load);
        Assert.Equal(PluginsLock.Path + ": " + message, err.Message);
    }

    [Fact]
    public void LoadRefusesTwice()
    {
        using var sb = new Sandbox();
        var e = $"[[plugin]]\nid = \"a\"\nsource = \"s\"\ncommit = \"{C1}\"\n";
        Sandbox.Write(sb.Home, ".config/mazapan/plugins.lock", e + e);
        Assert.Equal(PluginsLock.Path + ": a is there twice", Assert.Throws<MazapanException>(PluginsLock.Load).Message);
    }
}
