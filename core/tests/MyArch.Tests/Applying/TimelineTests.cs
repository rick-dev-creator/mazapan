using MyArch.Applying;
using MyArch.Config;

namespace MyArch.Tests.Applying;

public sealed class TimelineTests : IDisposable
{
    readonly TempDir dir = new();

    public void Dispose() => dir.Dispose();

    Settings Config(string text)
    {
        var path = dir[$"config-{Guid.NewGuid():N}.toml"];
        File.WriteAllText(path, text);
        return Settings.LoadFrom(path);
    }

    [Fact]
    public void NothingChangedIsNoChange()
    {
        var a = Config("theme = \"gruvbox\"\n[plugins.bar-volume]\nmax_volume = 1.25\n");
        var b = Config("theme = \"gruvbox\"\n[plugins.bar-volume]\nmax_volume = 1.25\n");
        Assert.Empty(Timeline.Diff(a, b));
    }

    [Fact]
    public void SaysWhatChangedInTheTermsItWasSetIn()
    {
        var a = Config("theme = \"gruvbox\"\ndisabled_plugins = [\"markets\"]\n[plugins.bar-volume]\nmax_volume = 1.25\nstep = 5\n");
        var b = Config("theme = \"paper\"\naccent = \"#ff8800\"\ndisabled_plugins = [\"theme-foot\"]\n[plugins.bar-volume]\nmax_volume = 1.5\n[plugins.idle]\nlock = 10\n");
        var d = Timeline.Diff(a, b);
        Assert.Contains(d, c => c is { Kind: "theme" } && (string?)c.From == "gruvbox" && (string?)c.To == "paper");
        Assert.Contains(d, c => c is { Kind: "accent", From: null } && (string?)c.To == "#ff8800");
        Assert.Contains(d, c => c is { Kind: "on", Plugin: "markets" });
        Assert.Contains(d, c => c is { Kind: "off", Plugin: "theme-foot" });
        // Set, changed, and back to the default (not set).
        Assert.Contains(d, c => c is { Kind: "setting", Plugin: "idle", Key: "lock", From: null } && Convert.ToInt64(c.To) == 10);
        Assert.Contains(d, c => c is { Kind: "setting", Plugin: "bar-volume", Key: "max_volume" } && Convert.ToDouble(c.To) == 1.5);
        Assert.Contains(d, c => c is { Kind: "setting", Plugin: "bar-volume", Key: "step", To: null } && Convert.ToInt64(c.From) == 5);
        Assert.Equal(7, d.Count);
    }

    [Fact]
    public void HardwarePluginsTurnOnByBeingEnabled()
    {
        var a = Config("theme = \"gruvbox\"\n");
        var b = Config("theme = \"gruvbox\"\nenabled_plugins = [\"hw-nvidia\"]\n");
        Assert.Equal([new Timeline.Change("on", "hw-nvidia")], Timeline.Diff(a, b));
        Assert.Equal([new Timeline.Change("off", "hw-nvidia")], Timeline.Diff(b, a));
    }

    [Fact]
    public void SameComparesTomlValues()
    {
        Assert.True(Timeline.Same(1.5, 1.5));
        Assert.True(Timeline.Same(null, null));
        Assert.False(Timeline.Same(null, 1L));
        Assert.False(Timeline.Same("a", "b"));
    }
}
