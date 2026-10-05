using Mazapan.Updates;

namespace Mazapan.Tests.Updates;

public class CodenameTests
{
    [Fact]
    public void EachReleaseHasItsName()
    {
        // The first is Mazapan itself; its patches and the builds after it too.
        Assert.Equal("Mazapan", Codename.For("0.1.0"));
        Assert.Equal("Mazapan", Codename.For("0.1.3"));
        Assert.Equal("Mazapan", Codename.For("0.1.0.r5.gabc1234"));
        // Before the first release, and past the list (a name not chosen yet): none.
        Assert.Equal("", Codename.For("0.2.0"));
        Assert.Equal("", Codename.For("0.0.0.r117.g67cec8e"));
        Assert.Equal("", Codename.For("7.0.0"));
        Assert.Equal("", Codename.For("garbage"));
    }

    [Fact]
    public void OneNameAVersion()
    {
        Assert.Equal(Codename.Names.Length, Codename.Names.Select(n => n.Version).Distinct().Count());
        Assert.Equal(Codename.Names.Length, Codename.Names.Select(n => n.Name).Distinct().Count());
    }
}
