using Mazapan.Cli;
using Mazapan.Install;
using Mazapan.Plugins;
using Mazapan.Tests.Foundation;
using Mazapan.Util;

namespace Mazapan.Tests.Install;

/// <summary>The tools for writing plugins, in a $HOME of their own, with the repository's themes and plugins.</summary>
[Collection("InstallHome")]
public class AuthorTests
{
    sealed class Home : IDisposable
    {
        readonly Sandbox sb = new();
        readonly string? oldRoot = Environment.GetEnvironmentVariable("MAZAPAN_ROOT");
        public string Root => sb.Root;
        public Home() => Environment.SetEnvironmentVariable("MAZAPAN_ROOT", Repo.Root);
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MAZAPAN_ROOT", oldRoot);
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

    /// <summary>plugins check ARG --json, read back.</summary>
    static (int Exit, System.Text.Json.JsonElement Json) CheckJson(string arg)
    {
        var o = Console.Out;
        var w = new StringWriter();
        Console.SetOut(w);
        try
        {
            var exit = Program.PluginsCheck(arg, json: true);
            return (exit, System.Text.Json.JsonDocument.Parse(w.ToString()).RootElement);
        }
        finally
        {
            Console.SetOut(o);
        }
    }

    static List<string> Strings(System.Text.Json.JsonElement j, string key) => [.. j.GetProperty(key).EnumerateArray().Select(x => x.GetString()!)];

    [Fact]
    public void CheckSaysWhatTheRegistryNeeds()
    {
        using var h = new Home();
        Program.PluginsNew("my-panel", "panel", "");
        var dir = Path.Join(Git.Dir, "my-panel");
        // Others' plugins say who made them and how they look.
        var (exit, j) = CheckJson("my-panel");
        Assert.Equal(0, exit);
        Assert.True(j.GetProperty("ok").GetBoolean());
        var warnings = Strings(j, "warnings");
        Assert.Contains(warnings, w => w.StartsWith("no author"));
        Assert.Contains(warnings, w => w.StartsWith("no icon"));
        Assert.Contains(warnings, w => w.StartsWith("no screenshots"));
        Assert.Equal("my-panel", j.GetProperty("id").GetString());
        Assert.NotEqual("", j.GetProperty("translations").GetProperty("es").GetProperty("name").GetString());
        Assert.Contains("es", Strings(j, "languages"));
        Assert.True(j.GetProperty("renders").GetInt32() > 1);

        // All said: nothing to warn about there.
        Directory.CreateDirectory(Path.Join(dir, "media"));
        File.WriteAllText(Path.Join(dir, "media", "icon.svg"), "<svg/>");
        File.WriteAllBytes(Path.Join(dir, "media", "panel.png"), new byte[1000]);
        var manifest = Path.Join(dir, "plugin.toml");
        var text = File.ReadAllText(manifest);
        var i = text.IndexOf("\n[", StringComparison.Ordinal);
        text = text[..i] + "\nauthor = \"Ana\"\nlicense = \"MIT\"\nhomepage = \"https://example.com\"\n\n[gallery]\nicon = \"media/icon.svg\"\nscreenshots = [\"media/panel.png\"]\n" + text[i..];
        File.WriteAllText(manifest, text);
        (exit, j) = CheckJson("my-panel");
        Assert.Equal(0, exit);
        Assert.DoesNotContain(Strings(j, "warnings"), w => w.StartsWith("no author") || w.StartsWith("no license") || w.StartsWith("no icon") || w.StartsWith("no screenshots"));
        Assert.Equal(("Ana", "MIT", "media/icon.svg"), (j.GetProperty("author").GetString(), j.GetProperty("license").GetString(), j.GetProperty("icon").GetString()));
        Assert.Equal(["media/panel.png"], Strings(j, "screenshots"));

        // A screenshot too big for the gallery, a built-in's key: errors.
        File.WriteAllBytes(Path.Join(dir, "media", "panel.png"), new byte[3 << 20]);
        var palette = (string)Plugin.Load(Path.Join(Repo.Root, "plugins", "palette")).Settings["key"];
        File.WriteAllText(manifest, System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(manifest), "(?m)^key = \".*\"$", $"key = \"{palette}\""));
        (exit, j) = CheckJson("my-panel");
        Assert.Equal(1, exit);
        var errors = Strings(j, "errors");
        Assert.Contains(errors, e => e.Contains("media/panel.png is 3072 KB: up to 2048 KB"));
        Assert.Contains(errors, e => e.Contains("palette.key's too"));

        // One that doesn't load: said as JSON too.
        File.WriteAllText(manifest, "[plugin]\nid = \"Bad\"\n");
        (exit, j) = CheckJson(dir);
        Assert.Equal(1, exit);
        Assert.False(j.GetProperty("ok").GetBoolean());
        Assert.Single(Strings(j, "errors"));
    }

    [Fact]
    public void NewDoesntTakeABuiltInsName()
    {
        using var h = new Home();
        var e = Assert.Throws<MazapanException>(() => Program.PluginsNew("bar-clock", "bar", ""));
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
        Assert.Throws<MazapanException>(() => Program.PluginsFork("bar-clock", "")); // it's yours already
        Program.PluginsDiff("bar-clock"); // the same: says so
        Assert.Throws<MazapanException>(() => Program.PluginsDiff("../x"));

        Program.PluginsFork("bar-clock", "my-clock");
        Assert.Equal("my-clock", Plugin.Load(Path.Join(Git.Dir, "my-clock")).Id);
        Assert.Throws<MazapanException>(() => Program.PluginsFork("bar-clock", "Bad Id"));
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
        Assert.Throws<MazapanException>(() => Program.Trusted(new PluginsLock(), Plugin.Load(link)));
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
