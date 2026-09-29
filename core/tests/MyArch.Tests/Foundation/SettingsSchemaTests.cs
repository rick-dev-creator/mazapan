using MyArch.Plugins;
using MyArch.Util;

namespace MyArch.Tests.Foundation;

public class SettingsSchemaTests
{
    static Plugin Load(TempDir d, string settings)
    {
        d.Write("p/plugin.toml", "[plugin]\nid = \"p\"\nversion = \"1.0\"\napi = 1\n\n[settings]\n" + settings);
        return Plugin.Load(Path.Join(d.Path, "p"));
    }

    [Fact]
    public void APlainSettingIsDescribedByItsCommentNameAndType()
    {
        using var d = new TempDir();
        var p = Load(d, "# How often, in minutes.\n# At least five.\nrefresh_minutes = 15\n\nlock_key = \"SUPER + L\"\nterminal = \"foot\"\nbar = true\n");
        var info = p.SettingsInfo["refresh_minutes"];
        Assert.Equal("Refresh minutes", info.Label);
        Assert.Equal("How often, in minutes. At least five.", info.Description);
        Assert.Equal("integer", info.Kind);
        Assert.Equal("key", p.SettingsInfo["lock_key"].Kind);
        Assert.Equal("command", p.SettingsInfo["terminal"].Kind);
        Assert.Equal("switch", p.SettingsInfo["bar"].Kind);
        Assert.Equal("", p.SettingsInfo["bar"].Description);
    }

    [Fact]
    public void ADescribedSettingKeepsItsDefaultAndRules()
    {
        using var d = new TempDir();
        var p = Load(d, "# Metric or imperial.\nunits = { default = \"metric\", choices = [\"metric\", \"imperial\"] }\nstep = { default = 0.05, min = 0.01, max = 0.25, label = \"Step\" }\n");
        Assert.Equal("metric", p.Settings["units"]);
        Assert.Equal("choice", p.SettingsInfo["units"].Kind);
        Assert.Equal("Metric or imperial.", p.SettingsInfo["units"].Description);
        Assert.Equal(0.25, p.SettingsInfo["step"].Max);
        Assert.Contains("one of", Assert.Throws<MyArchException>(() => p.Resolve(new Dictionary<string, object> { ["units"] = "kelvin" })).Message);
        Assert.Contains("at most", Assert.Throws<MyArchException>(() => p.Resolve(new Dictionary<string, object> { ["step"] = 0.5 })).Message);
        Assert.Equal("imperial", p.Resolve(new Dictionary<string, object> { ["units"] = "imperial" })["units"]);
    }

    [Theory]
    [InlineData("x = { default = 5, min = 10 }")]
    [InlineData("x = { default = \"a\", choices = [\"b\"] }")]
    [InlineData("x = { default = 1, kind = \"nope\" }")]
    [InlineData("x = { default = 1, colour = \"red\" }")]
    [InlineData("x = { default = \"on\", kind = \"switch\" }")]
    [InlineData("x = { default = 1, kind = \"choice\" }")]
    [InlineData("x = { default = 1.5, kind = \"integer\" }")]
    [InlineData("x = { default = 1, kind = \"text\" }")]
    [InlineData("x = { default = 1, min = 0, max = 2, step = 0 }")]
    public void AWrongSchemaIsAnError(string settings)
    {
        using var d = new TempDir();
        Assert.Throws<MyArchException>(() => Load(d, settings + "\n"));
    }

    [Fact]
    public void ATableSettingWithoutDefaultStaysATable()
    {
        using var d = new TempDir();
        var p = Load(d, "t = { a = 1 }\n");
        Assert.IsType<Tomlyn.Model.TomlTable>(p.Settings["t"]);
    }
}
