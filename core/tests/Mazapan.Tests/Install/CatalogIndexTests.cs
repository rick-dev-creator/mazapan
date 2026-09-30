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
    [InlineData("id = \"a\"\nsource = \"x\"\ncategories = [\"games\"]", "category \"games\"")]
    [InlineData("id = \"a\"\nsource = \"x\"\nlicence = \"MIT\"", "licence")]
    [InlineData("id = \"a\"\nsource = \"x\"\n[plugin.translations.spanish]\nname = \"A\"", "language code")]
    [InlineData("id = \"a\"\nsource = \"x\"\n[plugin.translations.es]\nnombre = \"A\"", "nombre")]
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
}
