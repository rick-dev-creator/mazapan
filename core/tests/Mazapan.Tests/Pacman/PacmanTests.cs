using Mazapan.Pacman;
using Mazapan.Util;

namespace Mazapan.Tests.Pacman;

public class PacmanTests
{
    [Fact]
    public void ParsePending()
    {
        var got = Packages.ParsePending("foot 1.28.0-2 -> 1.28.1-1\nhyprland 0.56.2-3 -> 0.57.0-1\ngarbage line\n");
        Assert.Equal(
            [new Change("foot", "1.28.0-2", "1.28.1-1"), new Change("hyprland", "0.56.2-3", "0.57.0-1")],
            got);
    }

    [Fact]
    public void Diff()
    {
        var before = new Dictionary<string, string> { ["a"] = "1", ["b"] = "1", ["gone"] = "1" };
        var after = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2", ["new"] = "1" };
        Assert.Equal(
            [new Change("b", "1", "2"), new Change("gone", From: "1"), new Change("new", To: "1")],
            Packages.Diff(before, after));
    }

    [Fact]
    public void CachedFile()
    {
        var dir = Directory.CreateTempSubdirectory("mazapan-test-").FullName;
        try
        {
            foreach (var f in new[]
            {
                "foot-1.28.0-2-x86_64.pkg.tar.zst",
                "foot-1.28.0-2-x86_64.pkg.tar.zst.sig",
                "foot-terminfo-1.28.0-2-x86_64.pkg.tar.zst",
                "go-2:1.27.1-1-x86_64.pkg.tar.zst",
            })
                File.WriteAllBytes(Path.Join(dir, f), []);
            Assert.Equal("foot-1.28.0-2-x86_64.pkg.tar.zst", Paths.Base(Packages.CachedFile([dir], "foot", "1.28.0-2") ?? ""));
            Assert.Equal("go-2:1.27.1-1-x86_64.pkg.tar.zst", Paths.Base(Packages.CachedFile([dir], "go", "2:1.27.1-1") ?? "")); // epoch
            Assert.Null(Packages.CachedFile([dir], "foot", "1.27.0-1")); // a version not in the cache must not match
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Theory]
    [InlineData("2:1.27.1-1", "2:1.27.1-1")]
    [InlineData("1.0+git/x?y;z,w", "1.0+git%2Fx%3Fy%3Bz%2Cw")]
    [InlineData("a b!é", "a%20b%21%C3%A9")]
    public void PathEscape(string s, string want) => Assert.Equal(want, Packages.PathEscape(s));
}

[System.Text.Json.Serialization.JsonSerializable(typeof(List<Change>))]
internal partial class PacmanJson : System.Text.Json.Serialization.JsonSerializerContext;

public class ChangeJsonTests
{
    [Fact]
    public void JsonAsGoWroteIt()
    {
        List<Change> changes = [new("a", "1", "2"), new("new", To: "1"), new("gone", From: "1")];
        var json = System.Text.Json.JsonSerializer.Serialize(changes, PacmanJson.Default.ListChange);
        Assert.Equal("""[{"name":"a","from":"1","to":"2"},{"name":"new","to":"1"},{"name":"gone","from":"1"}]""", json);
        Assert.Equal(changes, System.Text.Json.JsonSerializer.Deserialize(json, PacmanJson.Default.ListChange));
    }
}
