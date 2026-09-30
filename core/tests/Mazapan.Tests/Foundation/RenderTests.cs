using Mazapan.Plugins;
using Mazapan.Rendering;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Tests.Foundation;

public class RenderTests
{
    static Theme TestTheme()
    {
        var t = new Theme { Id = "t" };
        t.Colors = new() { ["accent"] = "#ffb000" };
        t.Motion.DurationMS = 180;
        t.Motion.ResponseMS = 400;
        t.Motion.Damping = 0.8;
        return t;
    }

    static Plugin Mk(string id, string dir)
    {
        var p = new Plugin { Dir = dir };
        p.Meta.Id = id;
        return p;
    }

    static string RenderOne(string tmpl)
    {
        using var d = new TempDir();
        d.Write("f.tmpl", tmpl);
        d.Write("locales/en.toml", "hello = \"hello\"\nbye = \"bye\"\n");
        d.Write("locales/es.toml", "hello = 'hola \"tú\"'\n");
        var p = Mk("p", d.Path);
        p.Targets.Add(new Target { Template = "f.tmpl", Output = Path.Join(d.Path, "out") });
        return Renderer.All([p], TestTheme(), _ => null, "es_MX").Files[0].Content;
    }

    [Theory]
    [InlineData("{{ c \"accent\" }}", "#ffb000")]
    [InlineData("{{ hex (c \"accent\") }}", "ffb000")]
    [InlineData("{{ rgb (c \"accent\") }}", "rgb(ffb000)")]
    [InlineData("{{ rgba (c \"accent\") 0.5 }}", "rgba(ffb00080)")]
    [InlineData("{{ cssa (c \"accent\") 0.36 }}", "rgba(255, 176, 0, 0.36)")]
    [InlineData("{{ speed 0.5 }}", "0.90")]
    // omega = 2*pi/0.4 = 15.708: k = omega^2, c = 2*z*omega
    [InlineData("{{ spring 1 0 }}", "mass = 1, stiffness = 246.74, dampening = 25.13")]
    [InlineData("{{ spring 1 1 }}", "mass = 1, stiffness = 246.74, dampening = 31.42")]
    [InlineData("{{ spring 0.5 0 }}", "mass = 1, stiffness = 986.96, dampening = 50.27")]
    [InlineData("{{ camel \"bright_magenta\" }}", "brightMagenta")]
    [InlineData("{{ camel \"bg\" }}", "bg")]
    [InlineData("{{ t \"hello\" }}", "hola \"tú\"")]
    [InlineData("{{ tq \"hello\" }}", "\"hola \\\"tú\\\"\"")]
    [InlineData("{{ t \"bye\" }}", "bye")] // missing in es: falls back to en
    [InlineData("{{ lang }} {{ lang_code }}", "es_MX es")]
    [InlineData("{{ quote \"a\\tb\" }}", "\"a\\tb\"")]
    [InlineData("{{ num 1.50 }} {{ pct 0.905 }}", "1.5 91")]
    public void Functions(string tmpl, string want) => Assert.Equal(want, RenderOne(tmpl));

    [Fact]
    public void UnknownTokenFails() =>
        Assert.Contains("no color \"acent\"", Assert.Throws<MazapanException>(() => RenderOne("{{ c \"acent\" }}")).Message);

    [Fact]
    public void UnknownNameFails() => Assert.Throws<MazapanException>(() => RenderOne("{{ theme.nope }}"));

    [Fact]
    public void NamesAreReadOnly() => Assert.Throws<MazapanException>(() => RenderOne("{{ c = 1 }}"));

    [Fact]
    public void AnEndlessLoopIsStopped()
    {
        var saved = Renderer.Timeout;
        Renderer.Timeout = TimeSpan.FromSeconds(1);
        try
        {
            Assert.Throws<MazapanException>(() => RenderOne("{{ while true }}x{{ end }}"));
        }
        finally
        {
            Renderer.Timeout = saved;
        }
    }

    [Fact]
    public void ChecksAreRendered()
    {
        using var d = new TempDir();
        d.Write("locales/en.toml", "check = 'bar loads'\n");
        var p = Mk("p", d.Path);
        p.Checks.Add(new Check { Name = "{{ t \"check\" }}", Run = "test {{ settings.n }} = 3" });
        p.Settings = new() { ["n"] = 3L };
        var c = Renderer.All([p], TestTheme(), _ => null, "en").Checks[0];
        Assert.Equal(new RenderedCheck("p", "bar loads", "test 3 = 3", 15, false), c);
    }

    /// <summary>A target with each goes into every directory its marker matches, and its template sees which one.</summary>
    [Fact]
    public void EachPlace()
    {
        using var d = new TempDir();
        foreach (var name in new[] { "a.default", "b.work", "Crash Reports" }) d.Dir("profiles/" + name);
        d.Write("profiles/a.default/prefs.js", "");
        d.Write("profiles/b.work/prefs.js", "");
        d.Write("plugin/f.tmpl", "{{ base place }}");
        var p = Mk("p", Path.Join(d.Path, "plugin"));
        p.Targets.Add(new Target { Template = "f.tmpl", Output = "chrome/x.css", Each = [Path.Join(d.Path, "profiles", "*", "prefs.js")] });
        var files = Renderer.All([p], TestTheme(), _ => null, "en").Files;
        Assert.Equal(2, files.Count);
        Assert.Equal(Path.Join(d.Path, "profiles", "a.default", "chrome", "x.css"), files[0].Path);
        Assert.Equal("b.work", files[1].Content);
    }

    [Fact]
    public void ActionsAreSeenByEveryTemplate()
    {
        using var d = new TempDir();
        d.Write("a/locales/en.toml", "open = 'Open monitors'\n");
        var a = Mk("a", Path.Join(d.Path, "a"));
        a.Actions.Add(new Mazapan.Plugins.Action { Name = "{{ t \"open\" }}", Run = "qs ipc call x", Key = "{{ settings.key }}" });
        // Its key unbound in config.toml, and no command: left out.
        a.Actions.Add(new Mazapan.Plugins.Action { Name = "only a key", Key = "{{ settings.other }}" });
        a.Settings = new() { ["key"] = "SUPER + M", ["other"] = "" };
        // "palette" must see a's actions whatever the order.
        d.Write("palette/p.tmpl", "{{ json actions }}");
        var pal = Mk("palette", Path.Join(d.Path, "palette"));
        pal.Targets.Add(new Target { Template = "p.tmpl", Output = Path.Join(d.Path, "out") });
        var got = Renderer.All([pal, a], TestTheme(), _ => null, "en").Files[0].Content;
        Assert.Equal("[{\"plugin\":\"a\",\"name\":\"Open monitors\",\"run\":\"qs ipc call x\",\"key\":\"SUPER + M\"}]", got);
    }

    [Theory]
    [InlineData(0.625, 2, "0.62")]
    [InlineData(0.635, 2, "0.64")]
    [InlineData(1.5, 2, "1.50")]
    [InlineData(-0.001, 2, "-0.00")]
    [InlineData(246.74011002723395, 2, "246.74")]
    public void GoFixed(double f, int prec, string want) => Assert.Equal(want, GoFormat.Fixed(f, prec));
}
