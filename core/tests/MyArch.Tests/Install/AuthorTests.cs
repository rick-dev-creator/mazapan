using MyArch.Cli;
using MyArch.Install;
using MyArch.Plugins;
using MyArch.Tests.Foundation;
using MyArch.Util;

namespace MyArch.Tests.Install;

/// <summary>The tools for writing plugins, in a $HOME of their own, with the repository's themes and plugins.</summary>
[Collection("InstallHome")]
public class AuthorTests
{
    sealed class Home : IDisposable
    {
        readonly Sandbox sb = new();
        readonly string? oldRoot = Environment.GetEnvironmentVariable("MYARCH_ROOT");
        public string Root => sb.Root;
        public Home() => Environment.SetEnvironmentVariable("MYARCH_ROOT", Repo.Root);
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MYARCH_ROOT", oldRoot);
            sb.Dispose();
        }
    }

    [Theory]
    [InlineData("bar")]
    [InlineData("panel")]
    [InlineData("window")]
    [InlineData("theme")]
    [InlineData("tools")]
    public void EveryKindStartsAsAPluginThatPassesCheck(string kind)
    {
        using var h = new Home();
        Program.PluginsNew("my-" + kind, kind, "");
        var dir = Path.Join(Git.Dir, "my-" + kind);
        Assert.True(File.Exists(Path.Join(dir, "plugin.toml")));
        Assert.True(File.Exists(Path.Join(dir, "locales", "es.toml")));
        Assert.Equal(0, Program.PluginsCheck("my-" + kind));
        // Described in both languages: nothing to warn about.
        var p = Plugin.Load(dir);
        Assert.All(p.Settings.Keys, k => Assert.NotEqual("", p.SettingsInfo[k].Description));
    }

    [Fact]
    public void NewDoesntTakeABuiltInsName()
    {
        using var h = new Home();
        var e = Assert.Throws<MyArchException>(() => Program.PluginsNew("bar-clock", "bar", ""));
        Assert.Contains("plugins fork bar-clock", e.Message);
    }

    [Fact]
    public void CheckFailsOnAMistakeInAnyTheme()
    {
        using var h = new Home();
        Program.PluginsNew("my-bar", "bar", "");
        var t = Path.Join(Git.Dir, "my-bar", "Widget.qml.tmpl");
        File.WriteAllText(t, File.ReadAllText(t).Replace("Theme.fgMuted", "\"{{ c \\\"nope\\\" }}\""));
        Assert.Equal(1, Program.PluginsCheck("my-bar"));
    }

    [Fact]
    public void ForkTakesTheBuiltInsPlaceOrANewId()
    {
        using var h = new Home();
        Program.PluginsFork("bar-clock", "");
        var mine = Path.Join(Git.Dir, "bar-clock");
        Assert.True(File.Exists(Path.Join(mine, "plugin.toml")));
        Assert.Throws<MyArchException>(() => Program.PluginsFork("bar-clock", "")); // it's yours already
        Program.PluginsDiff("bar-clock"); // the same: says so
        Assert.Throws<MyArchException>(() => Program.PluginsDiff("../x"));

        Program.PluginsFork("bar-clock", "my-clock");
        Assert.Equal("my-clock", Plugin.Load(Path.Join(Git.Dir, "my-clock")).Id);
        Assert.Throws<MyArchException>(() => Program.PluginsFork("bar-clock", "Bad Id"));
    }

    [Fact]
    public void ALinkedCheckoutIsTheAuthorsButACopiedOneIsNot()
    {
        using var h = new Home();
        var src = Path.Join(h.Root, "src", "mine");
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("mine"));
        Sandbox.Git(src, "init", "--quiet", "-b", "main");
        Directory.CreateDirectory(Git.Dir);
        var link = Path.Join(Git.Dir, "mine");
        Directory.CreateSymbolicLink(link, src);
        Program.Trusted(new PluginsLock(), Plugin.Load(link));

        // The same checkout, copied in: nobody approved it.
        File.Delete(link);
        Directory.CreateDirectory(link);
        File.Copy(Path.Join(src, "plugin.toml"), Path.Join(link, "plugin.toml"));
        Directory.CreateDirectory(Path.Join(link, ".git"));
        Assert.Throws<MyArchException>(() => Program.Trusted(new PluginsLock(), Plugin.Load(link)));
    }

    [Fact]
    public void RemovingALinkInABuiltInsPlaceLeavesTheFolderAndTheBuiltIn()
    {
        using var h = new Home();
        var src = Path.Join(h.Root, "src", "clock");
        Sandbox.Write(src, "plugin.toml", Sandbox.Manifest("bar-clock"));
        Directory.CreateDirectory(Git.Dir);
        var link = Path.Join(Git.Dir, "bar-clock");
        Directory.CreateSymbolicLink(link, src);
        Program.PluginsRemove(["bar-clock"]);
        Assert.False(Paths.Exists(link));
        Assert.True(File.Exists(Path.Join(src, "plugin.toml")));
    }

    [Theory]
    [InlineData("https://h/a/b", "https://h/a/b/", true)]
    [InlineData("https://h/a/b.git", "https://h/a/b", true)]
    [InlineData("https://h/a/b", "https://h/a/c", false)]
    public void SameSource(string a, string b, bool same) => Assert.Equal(same, Program.SameSource(a, b));
}
