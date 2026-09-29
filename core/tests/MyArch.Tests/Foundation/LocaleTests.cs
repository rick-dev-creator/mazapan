using MyArch.Locale;
using MyArch.Util;

namespace MyArch.Tests.Foundation;

public class LocaleTests
{
    [Theory]
    [InlineData("es_MX.UTF-8", "es_MX")]
    [InlineData("de_DE.UTF-8@euro", "de_DE")]
    [InlineData("en", "en")]
    [InlineData("C", "")]
    [InlineData("POSIX", "")]
    [InlineData("", "")]
    public void Clean(string input, string want) => Assert.Equal(want, Languages.Clean(input));

    [Fact]
    public void DetectOrder()
    {
        var saved = new[] { "LC_ALL", "LC_MESSAGES", "LANG" }.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("LC_ALL", "");
            Environment.SetEnvironmentVariable("LC_MESSAGES", "fr_FR.UTF-8");
            Environment.SetEnvironmentVariable("LANG", "es_MX.UTF-8");
            Assert.Equal("fr_FR", Languages.Detect("")); // LC_MESSAGES wins over LANG
            Assert.Equal("pt_BR", Languages.Detect("pt_BR")); // config override wins
        }
        finally
        {
            foreach (var (k, v) in saved) Environment.SetEnvironmentVariable(k, v);
        }
    }

    [Theory]
    [InlineData("es_MX", "es_MX,es,en")]
    [InlineData("es", "es,en")]
    [InlineData("en_US", "en_US,en")]
    [InlineData("", "en")]
    public void Candidates(string lang, string want) => Assert.Equal(want, string.Join(",", Languages.Candidates(lang)));

    static string WriteLocales(TempDir d, Dictionary<string, string> files)
    {
        foreach (var (name, body) in files) d.Write($"locales/{name}.toml", body);
        return d.Path;
    }

    [Fact]
    public void FallbackChain()
    {
        using var d = new TempDir();
        var dir = WriteLocales(d, new()
        {
            ["en"] = "a = 'A'\nb = 'B'\nc = 'C'\n",
            ["es"] = "a = 'A-es'\nb = 'B-es'\n",
            ["es_MX"] = "a = 'A-mx'\n",
        });
        var c = Catalog.Load("p", dir, "es_MX");
        Assert.Equal("A-mx", c.T("a"));
        Assert.Equal("B-es", c.T("b"));
        Assert.Equal("C", c.T("c"));
        Assert.Throws<MyArchException>(() => c.T("nope"));
    }

    [Fact]
    public void KeyNotInEnglishIsAnError()
    {
        using var d = new TempDir();
        var dir = WriteLocales(d, new() { ["en"] = "hello = 'hello'\n", ["es"] = "helo = 'hola'\n" });
        var e = Assert.Throws<MyArchException>(() => Catalog.Load("p", dir, "es"));
        Assert.Contains("locales/es.toml has \"helo\"", e.Message);
    }

    [Fact]
    public void WhatTheManifestSaysNeedsNoEnglishKey()
    {
        // plugin.toml has the English name, description and settings' labels.
        using var d = new TempDir();
        var dir = WriteLocales(d, new() { ["en"] = "hello = 'hello'\n", ["es"] = "'plugin.description' = 'Hola'\n'setting.x' = 'Equis'\n" });
        var c = Catalog.Load("p", dir, "es");
        Assert.Equal("Hola", c.TryT("plugin.description"));
        Assert.Null(c.TryT("plugin.name"));
    }
}
