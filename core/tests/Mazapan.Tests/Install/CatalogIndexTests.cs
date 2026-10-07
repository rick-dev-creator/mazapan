using Mazapan.Install;
using Mazapan.Tests.Foundation;

namespace Mazapan.Tests.Install;

public class CatalogIndexTests
{
    const string One = """
        [[plugin]]
        id = "bar-uptime"
        name = "Uptime"
        description = "Uptime in the bar"
        source = "https://example.com/bar-uptime.git"
        ref = "v1.0"
        categories = ["bar"]
        [plugin.translations.es]
        description = "El tiempo encendido, en la barra"
        """;

    [Fact]
    public void EntriesAreRead()
    {
        var e = Assert.Single(CatalogIndex.Parse(One, "c.toml"));
        Assert.Equal(("bar-uptime", "v1.0", "c.toml"), (e.Id, e.Ref, e.From));
        Assert.Equal(["bar"], e.Categories);
        // Translated what there is; the rest as written.
        Assert.Equal(("Uptime", "El tiempo encendido, en la barra"), e.In("es_MX"));
        Assert.Equal(("Uptime", "Uptime in the bar"), e.In("de"));
    }

    [Fact]
    public void EveryEntryHasItsTranslations()
    {
        var two = One.Replace("[plugin.translations.es]\ndescription", "translations.es.description")
            + "\n[[plugin]]\nid = \"gaps\"\nsource = \"x\"\ntranslations.es.name = \"Huecos\"\n";
        var es = CatalogIndex.Parse(two, "c.toml").Select(e => e.In("es").Name + "|" + e.In("es").Description);
        Assert.Equal(["Uptime|El tiempo encendido, en la barra", "Huecos|"], es);
    }

    [Theory]
    [InlineData("id = \"Bad Id\"\nsource = \"x\"", "plugin id")]
    [InlineData("id = \"a\"", "source")]
    [InlineData("id = \"a\"\nsource = \"--upload-pack=x\"", "source")]
    [InlineData("id = \"a\"\nsource = \"x\"\nref = \"--x\"", "ref")]
    [InlineData("id = \"a\"\nsource = \"x\"\ncommit = \"abc123\"", "full commit")]
    [InlineData("id = \"a\"\nsource = \"x\"\ncommit = \"--upload-pack=x\"", "full commit")]
    [InlineData("id = \"a\"\nsource = \"x\"\n[plugin.translations.spanish]\nname = \"A\"", "language code")]
    public void BadEntriesAreRefused(string entry, string says)
    {
        var e = Assert.ThrowsAny<Exception>(() => CatalogIndex.Parse("[[plugin]]\n" + entry + "\n", "c.toml"));
        Assert.Contains(says, e.Message);
    }

    [Fact]
    public void TheFirstCatalogWinsAndProblemsDontStopTheRest()
    {
        using var d = new TempDir();
        var a = d.Write("a.toml", One);
        var b = d.Write("b.toml", One.Replace("Uptime in the bar", "Other") + "\n[[plugin]]\nid = \"gaps\"\nsource = \"x\"\n");
        var bad = d.Write("bad.toml", "[[plugin]]\nid = 1\n");
        var (entries, problems) = CatalogIndex.Load([a, bad, b, Path.Join(d.Path, "missing.toml")], false);
        Assert.Equal(["bar-uptime", "gaps"], entries.Select(e => e.Id));
        Assert.Equal("Uptime in the bar", entries[0].Description);
        Assert.Equal("gaps", entries[1].Name); // no name: its id
        // The broken one, and the one that isn't there.
        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, x => x.Contains("no such file"));
    }

    [Fact]
    public void ACatalogOverPlainHttpIsRefused()
    {
        var (entries, problems) = CatalogIndex.Load(["http://example.com/index.toml"], false);
        Assert.Empty(entries);
        Assert.Contains("not https", Assert.Single(problems));
    }

    [Fact]
    public void WhatTheRegistryAddsIsRead()
    {
        var commit = new string('a', 40);
        var e = Assert.Single(CatalogIndex.Parse(
            One.Replace("categories = [\"bar\"]", $"categories = [\"bar\"]\ncommit = \"{commit}\"\nversion = \"1.2.0\"\nlicense = \"MIT\""), "c.toml"));
        Assert.Equal((commit, "1.2.0", "MIT"), (e.Commit, e.Version, e.License));
    }

    [Fact]
    public void WhatANewerMazapanWritesIsLeftAlone()
    {
        // A catalog is read by every Mazapan out there: one written for a newer
        // one (keys, tables, categories this one doesn't know) still lists
        // its plugins here.
        var newer = One.Replace("categories = [\"bar\"]", "categories = [\"bar\", \"games\"]\nlicence = \"MIT\"\nrequires_mazapan = \"9.0\"")
            + "\n[plugin.translations.es.extra]\nnombre = \"A\"\n[plugin.screenshots]\nmain = \"x.png\"\n";
        var e = Assert.Single(CatalogIndex.Parse("generated = 2026-10-07\n" + newer, "c.toml"));
        Assert.Equal(["bar"], e.Categories);
        Assert.Equal(("Uptime", "El tiempo encendido, en la barra"), e.In("es"));
    }

    [Fact]
    public void TheShippedCopyStandsInForTheRegistryOnlyWhenItCantBeRead()
    {
        using var d = new TempDir();
        var shipped = d.Write("shipped.toml", One);
        var other = d.Write("other.toml", "[[plugin]]\nid = \"gaps\"\nsource = \"x\"\n");
        var registry = Path.Join(d.Path, "registry.toml");
        // The registry can't be read: the copy shipped with mazapan, said.
        var (entries, problems) = CatalogIndex.Load([registry, other], false, shipped);
        Assert.Equal(["bar-uptime", "gaps"], entries.Select(e => e.Id));
        Assert.Contains("registry.toml", Assert.Single(problems));
        // It can: only what it lists (a plugin taken out of it is out).
        d.Write("registry.toml", "[[plugin]]\nid = \"other\"\nsource = \"y\"\n");
        (entries, problems) = CatalogIndex.Load([registry, other], false, shipped);
        Assert.Equal(["other", "gaps"], entries.Select(e => e.Id));
        Assert.Empty(problems);
        // Another catalog that can't be read isn't stood in for.
        (entries, problems) = CatalogIndex.Load([registry, Path.Join(d.Path, "missing.toml")], false, shipped);
        Assert.Equal(["other"], entries.Select(e => e.Id));
        Assert.Single(problems);
        // A broken copy is said, not thrown.
        var broken = d.Write("broken.toml", "[[plugin]]\nid = 1\n");
        (entries, problems) = CatalogIndex.Load([Path.Join(d.Path, "missing.toml")], false, broken);
        Assert.Empty(entries);
        Assert.Equal(2, problems.Count);
    }
}
