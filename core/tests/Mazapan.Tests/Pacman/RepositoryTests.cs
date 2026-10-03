using Mazapan.Pacman;
using Mazapan.Util;

namespace Mazapan.Tests.Pacman;

public class RepositoryTests
{
    static string Root(string toml)
    {
        var dir = Directory.CreateTempSubdirectory("mazapan-test-").FullName;
        Directory.CreateDirectory(Path.Join(dir, "pkg"));
        File.WriteAllText(Path.Join(dir, "pkg", "repository.toml"), toml);
        return dir;
    }

    [Fact]
    public void NotPublishedIsEmpty()
    {
        Assert.Equal("", Repository.Server(Root("server = \"\"\n")));
        Assert.Equal("", Repository.Server(Directory.CreateTempSubdirectory("mazapan-test-").FullName));
    }

    [Fact]
    public void TheServerKeepsItsPlaceholders() =>
        Assert.Equal("https://example.org/$channel/$arch", Repository.Server(Root("server = \"https://example.org/$channel/$arch\"\n")));

    [Theory]
    [InlineData("http://example.org/$channel")]           // not https
    [InlineData("https://example.org/a b")]               // a space
    [InlineData("https://example.org/'$(reboot)'")]       // a quote
    [InlineData("https://example.org/\nServer = x")]      // a second line
    public void ABadServerIsRefused(string server) =>
        Assert.Throws<MazapanException>(() => Repository.Server(Root($"server = \"{server.Replace("\n", "\\n")}\"\n")));

    [Fact]
    public void AnUnknownKeyIsRefused() =>
        Assert.Throws<MazapanException>(() => Repository.Server(Root("sever = \"https://x\"\n")));

    [Fact]
    public void TheMirrorlistSaysItsChannel()
    {
        var text = Repository.MirrorlistText("https://example.org/$channel/$arch", "edge");
        Assert.Contains("\nServer = https://example.org/edge/$arch\n", text);
        Assert.Equal("edge", Repository.ChannelOf(text));
        Assert.Null(Repository.ChannelOf("Server = https://example.org/edge/$arch\n"));
        Assert.Null(Repository.ChannelOf("# mazapan channel: nightly\n"));
        Assert.Throws<MazapanException>(() => Repository.MirrorlistText("https://x/$channel", "dev"));
    }

    [Fact]
    public void Enabled()
    {
        Assert.True(Repository.Enabled("[core]\nInclude = x\n\n[mazapan]\nInclude = /etc/pacman.d/mazapan-mirrorlist\n"));
        Assert.False(Repository.Enabled("[core]\nInclude = x\n#[mazapan]\n"));
    }

    const string Changelog = """
        # Changelog

        Intro text.

        ## Unreleased

        - Something on edge,
          wrapped.

        ## 0.3.0 — 2026-11-01

        - Third.
        - Also third.

        ## 0.2.0 — 2026-10-15

        - Second.
        """;

    [Fact]
    public void WhatsNewIsWhatCameAfterTheInstalledVersion()
    {
        // To an edge build: what's unreleased too.
        var got = Repository.WhatsNew(Changelog, "0.2.0", "0.3.0.r2.gabc1234");
        Assert.Equal(["Unreleased", "0.3.0"], got.Select(r => r.Version));
        Assert.Equal(["Something on edge, wrapped."], got[0].Items);
        Assert.Equal(["Third.", "Also third."], got[1].Items);
        // To a release: only up to it, nothing unreleased.
        Assert.Equal(["0.3.0"], Repository.WhatsNew(Changelog, "0.2.0", "0.3.0").Select(r => r.Version));
        Assert.Equal(["0.2.0"], Repository.WhatsNew(Changelog, "0.1.0", "0.2.0").Select(r => r.Version));
        // Between releases (edge), from its release on.
        Assert.Equal(["Unreleased"], Repository.WhatsNew(Changelog, "0.3.0.r4.gabc1234", "0.3.0.r6.gdef5678").Select(r => r.Version));
        // Older than anything listed: all of it.
        Assert.Equal(3, Repository.WhatsNew(Changelog, "0.1.0", "0.3.0.r1.gabc").Count);
        // What a terminal would take as an escape sequence goes.
        Assert.Equal(["x]0;titley"], Repository.WhatsNew("## 0.2.0\n- x\u001b]0;title\u0007y\n", "0.1.0", "0.2.0")[0].Items);
    }
}

public class PacmanLogTests
{
    [Fact]
    public void AFailedInitramfsIsFound()
    {
        const string ok = """
            [2026-10-03T10:00:00-0600] [ALPM] running '60-mkinitcpio-remove.hook'...
            [2026-10-03T10:00:01-0600] [ALPM] running '90-mkinitcpio-install.hook'...
            [2026-10-03T10:00:01-0600] [ALPM-SCRIPTLET] ==> Building image from preset: /etc/mkinitcpio.d/linux.preset: 'default'
            [2026-10-03T10:00:02-0600] [ALPM-SCRIPTLET] ==> WARNING: Possibly missing firmware for module: 'qla2xxx'
            [2026-10-03T10:00:05-0600] [ALPM-SCRIPTLET] ==> Image generation successful
            [2026-10-03T10:00:06-0600] [ALPM] running 'dkms-install.hook'...
            [2026-10-03T10:00:06-0600] [ALPM-SCRIPTLET] ==> ERROR: something else's error
            """;
        Assert.Null(Packages.InitramfsFailed(ok));
        // An error it builds through (the image is still made): not a failure.
        Assert.Null(Packages.InitramfsFailed(ok.Replace("WARNING: Possibly missing firmware", "ERROR: module not found")));
        Assert.Null(Packages.InitramfsFailed(""));
        const string bad = """
            [2026-10-03T10:00:01-0600] [ALPM] running '90-mkinitcpio-install.hook'...
            [2026-10-03T10:00:01-0600] [ALPM-SCRIPTLET] ==> Building image from preset: /etc/mkinitcpio.d/linux.preset: 'default'
            [2026-10-03T10:00:02-0600] [ALPM-SCRIPTLET] ==> ERROR: module not found: 'nvidia'
            [2026-10-03T10:00:05-0600] [ALPM-SCRIPTLET] ==> Image generation FAILED: 'default'
            """;
        Assert.Equal("==> ERROR: module not found: 'nvidia'", Packages.InitramfsFailed(bad));
    }
}
