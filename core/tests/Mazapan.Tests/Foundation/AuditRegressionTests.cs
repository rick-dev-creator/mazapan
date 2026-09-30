using System.Diagnostics;
using Mazapan.Applying;
using Mazapan.Plugins;
using Mazapan.Rendering;
using Mazapan.Util;

namespace Mazapan.Tests.Foundation;

/// <summary>What the audit of the C# port found, kept from coming back.</summary>
public class AuditRegressionTests
{
    [Theory]
    [InlineData("../../secret.tmpl")]
    [InlineData("sub/x.tmpl")]
    [InlineData(".hidden.tmpl")]
    [InlineData("notes.txt")]
    public void ATemplateIsAFileOfThePlugin(string template)
    {
        using var d = new TempDir();
        d.Write("secret.tmpl", "key");
        d.Write("p/plugin.toml", $"[plugin]\nid = \"p\"\nversion = \"1.0\"\napi = 1\n\n[[targets]]\ntemplate = \"{template}\"\noutput = \"~/x\"\n");
        var e = Assert.Throws<MazapanException>(() => Plugin.Load(Path.Join(d.Path, "p")));
        Assert.Contains("a *.tmpl file in the plugin's folder", e.Message);
    }

    [Fact]
    public void ASharedFileThatIsNotUtf8IsLeftAlone()
    {
        using var d = new TempDir();
        var path = Path.Join(d.Path, "kdeglobals");
        byte[] latin1 = [.. "[General]\nName=Canci"u8.ToArray(), 0xF3, .. "n\n[Colors]\nBg=1\n"u8.ToArray()];
        File.WriteAllBytes(path, latin1);
        var f = new RenderedFile { Plugin = "p", Path = path, Content = "[Colors]\nBg=2\n", Merge = "ini" };
        var (changes, _) = Apply.Plan([f], new Owned());
        Assert.Equal(State.Unreadable, changes[0].State);
        Apply.Execute(changes, [], new Owned(), adopt: true);
        Assert.Equal(latin1, File.ReadAllBytes(path));
    }

    [Fact]
    public void ADanglingSymlinkIsReplaced()
    {
        using var d = new TempDir();
        var path = Path.Join(d.Path, "foot.ini");
        File.CreateSymbolicLink(path, Path.Join(d.Path, "gone", "foot.ini"));
        Files.WriteAtomic(path, "x");
        Assert.Equal("x", File.ReadAllText(path));
        Assert.Null(new FileInfo(path).LinkTarget);
    }

    [Fact]
    public void AChildHoldingTheOutputDoesNotHangAReload()
    {
        var clock = Stopwatch.StartNew();
        var err = Apply.RunShell("sleep 5 & echo hi", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1));
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3), $"took {clock.Elapsed}");
        Assert.NotNull(err); // Go: "exec: WaitDelay expired before I/O complete"
    }
}
