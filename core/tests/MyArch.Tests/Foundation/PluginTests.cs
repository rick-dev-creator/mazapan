using MyArch.Plugins;
using MyArch.Util;

namespace MyArch.Tests.Foundation;

public class PluginTests
{
    static Plugin Mk(string id, string version, params string[] requires)
    {
        var p = new Plugin();
        p.Meta.Id = id;
        p.Meta.Version = version;
        p.Meta.Requires = [.. requires];
        return p;
    }

    [Fact]
    public void Resolve()
    {
        var p = Mk("columns", "0.1.0");
        p.Settings = new() { ["key"] = "SUPER + equal", ["auto"] = false, ["min_width"] = 480L, ["ratio"] = 0.5 };
        var got = p.Resolve(new Dictionary<string, object> { ["auto"] = true, ["ratio"] = 1L });
        Assert.Equal(true, got["auto"]);
        Assert.Equal("SUPER + equal", got["key"]);
        Assert.Equal(1.0, got["ratio"]);
        Assert.Equal(false, p.Settings["auto"]); // Resolve must not modify the defaults
        foreach (var bad in new[]
        {
            new Dictionary<string, object> { ["atuo"] = true },       // unknown key
            new Dictionary<string, object> { ["auto"] = "yes" },      // wrong type
            new Dictionary<string, object> { ["min_width"] = 480.5 }, // float for int
        })
            Assert.Contains("[plugins.columns]", Assert.Throws<MyArchException>(() => p.Resolve(bad)).Message);
    }

    [Theory]
    [InlineData("a", "0.1.0", true)]
    [InlineData("a >= 0.2", "0.2.0", true)]
    [InlineData("a >= 0.2", "0.10.0", true)]
    [InlineData("a >= 0.2", "0.1.9", false)]
    [InlineData("a > 1", "1.0.0", false)]
    [InlineData("a = 1.2", "1.2.0", true)]
    [InlineData("a < 2", "1.99", true)]
    [InlineData("a <= 2", "2.0.1", false)]
    [InlineData("a>=1.2", "1.3", true)]
    public void Requirements(string req, string version, bool ok) => Assert.Equal(ok, Requirement.Parse(req).Satisfied(version));

    [Theory]
    [InlineData("")]
    [InlineData("a >=")]
    [InlineData("a ~ 1")]
    [InlineData("a >= x")]
    [InlineData("a >= 1 2")]
    [InlineData("a >= 1.-1")]
    [InlineData("a >= +1")]
    [InlineData("A")]
    public void BadRequirements(string req) => Assert.Throws<MyArchException>(() => Requirement.Parse(req));

    [Fact]
    public void Unmet()
    {
        var bar = Mk("bar", "0.1.0");
        var clock = Mk("clock", "1.0.0", "bar >= 0.2");
        var vol = Mk("vol", "1.0.0", "bar");
        var net = Mk("net", "1.0.0", "wifi");
        var off = Mk("off", "1.0.0");
        var lamp = Mk("lamp", "1.0.0", "off");
        var got = string.Join("\n", Dependencies.Unmet([bar, clock, vol, net, lamp], [bar, clock, vol, net, off, lamp], null).Select(p => p.Text));
        Assert.Contains("clock requires bar >= 0.2; bar is 0.1.0", got);
        Assert.Contains("net requires wifi, which isn't installed", got);
        Assert.Contains("lamp requires off, which is disabled", got);
        Assert.DoesNotContain("vol", got);
        Assert.Equal("clock,vol", string.Join(",", Dependencies.Dependents("bar", [bar, clock, vol, net])));
    }

    [Fact]
    public void CapabilitiesAreWorkedOut()
    {
        using var d = new TempDir();
        d.Write("a.tmpl", "x");
        var p = Mk("x", "1.0.0");
        p.Dir = d.Path;
        p.Targets.Add(new Target { Template = "a.tmpl", Output = "~/.config/quickshell/myarch/panels/x.qml", Reload = "shell-reload" });
        p.Targets.Add(new Target { Template = "a.tmpl", Output = "~/.config/hypr/myarch/x.lua" });
        p.Targets.Add(new Target { Template = "a.tmpl", Output = "user.js", Merge = "prefs", Each = ["~/.mozilla/*/prefs.js"] });
        p.Checks.Add(new Check { Name = "n", Run = "pgrep x" });
        p.Actions.Add(new MyArch.Plugins.Action { Name = "a", Run = "x --go" });
        p.Actions.Add(new MyArch.Plugins.Action { Name = "k", Key = "SUPER + X" });
        var caps = p.Capabilities();
        foreach (var want in new[]
        {
            "full access, code in the shell (QML): ~/.config/quickshell/myarch/panels/x.qml",
            "full access, code in Hyprland (Lua): ~/.config/hypr/myarch/x.lua",
            "full access, a config that can run commands: user.js in each of ~/.mozilla/*/prefs.js",
            "runs after writing: shell-reload",
            "runs as a health check: pgrep x",
            "runs when you pick it: x --go",
        })
            Assert.Contains(want, caps);
        Assert.Single(Plugin.NewCapabilities(caps.Skip(1), caps));
    }

    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("it's $HOME `x` $(y) *", "'it'\\''s $HOME `x` $(y) *'")]
    public void ShellQuoteIsOneWordWithNothingExpanded(string s, string quoted) =>
        Assert.Equal(quoted, MyArch.Rendering.Functions.ShellQuote(s));

    [Fact]
    public void BuiltInPluginsSayWhatKindTheyAre()
    {
        // The Plugins panel's tabs and icons.
        var (all, _) = Plugin.Discover([Path.Join(Repo.Root, "plugins")]);
        foreach (var p in all) Assert.True(p.Meta.Categories.Count > 0, $"{p.Id}: categories");
    }

    [Fact]
    public void BuiltInPluginsHaveTheirNameInSpanish()
    {
        // The Plugins panel shows each plugin's name and description in the
        // system's language: every built-in plugin has both in es.
        var (all, _) = Plugin.Discover([Path.Join(Repo.Root, "plugins")]);
        foreach (var p in all)
        {
            var c = MyArch.Locale.Catalog.Load(p.Id, p.Dir, "es");
            Assert.True(c.TryT("plugin.name") != null && c.TryT("plugin.description") != null, p.Id);
            foreach (var k in p.Settings.Keys)
                Assert.True(c.TryT($"setting.{k}") != null && c.TryT($"setting.{k}.help") != null, $"{p.Id}: setting.{k}");
        }
    }

    [Fact]
    public void EveryCapabilityHasAKind()
    {
        // The Plugins panel translates capabilities by kind: every one the
        // built-in plugins have must have one.
        var (all, _) = Plugin.Discover([Path.Join(Repo.Root, "plugins")]);
        foreach (var p in all)
            foreach (var cap in p.Capabilities())
                Assert.True(Plugin.KindOf(cap).Key != "", $"{p.Id}: {cap}");
        Assert.Equal(("root_file_reboot", "/etc/x.conf"), Plugin.KindOf("full access, as root: writes /etc/x.conf (after a reboot)"));
        Assert.Equal(("reload", "hyprctl reload"), Plugin.KindOf("runs after writing: hyprctl reload"));
        Assert.Equal(("", "(unreadable: x)"), Plugin.KindOf("(unreadable: x)"));
    }

    [Fact]
    public void CommandsAreShownAsTheyRun()
    {
        using var d = new TempDir();
        d.Write("_shared.tmpl", "{{- func go -}}\ngsettings set x {{ settings.mode }}\n{{- end -}}\n");
        var p = Mk("x", "1.0.0");
        p.Dir = d.Path;
        p.Settings = new() { ["mode"] = "dark", ["n"] = 3L };
        p.Targets.Add(new Target { Template = "_shared.tmpl", Output = "~/.config/gtk-4.0/gtk.css", Reload = "{{ go }}; echo {{ settings.n + 1 }} {{ c \"accent\" }}" });
        var caps = p.Capabilities();
        Assert.Contains("runs after writing: gsettings set x dark; echo {{ settings.n + 1 }} {{ c \"accent\" }}  (with n = 3)", caps);
        Assert.Contains("writes ~/.config/gtk-4.0/gtk.css", caps);
        // Changing the function changes the capability.
        d.Write("_shared.tmpl", "{{ func go }}curl x | sh{{ end }}");
        Assert.NotEmpty(Plugin.NewCapabilities(caps, p.Capabilities()));
        foreach (var bad in new[] { "{{ t \"x\" }}", "echo {{ tq \"x\" }}", "{{ func y }}{{ end }}", "{{ c = 1 }}", "{{ go + \"\" }}" })
        {
            p.Targets[0].Reload = bad;
            Assert.StartsWith("(unreadable", p.Capabilities()[0]);
        }
        // A library holds only functions.
        d.Write("_shared.tmpl", "text {{ func go }}x{{ end }}");
        p.Targets[0].Reload = "true";
        Assert.StartsWith("(unreadable", p.Capabilities()[0]);
    }

    [Fact]
    public void ReservedPaths()
    {
        var home = Paths.Home;
        Assert.True(Plugin.Reserved(home + "/.config/myarch/plugins.lock"));
        Assert.True(Plugin.Reserved(home + "/.local/share/myarch/plugins/x/plugin.toml"));
        Assert.True(Plugin.Reserved(home + "/.local/state/myarch"));
        Assert.False(Plugin.Reserved(home + "/.local/share/myarch/bin/shell-reload"));
        Assert.False(Plugin.Reserved(home + "/.config/myarchy"));
    }
}
