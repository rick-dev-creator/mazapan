using Mazapan.Store;
using Mazapan.Util;

namespace Mazapan.Tests.Store;

public class VendorTests
{
    static string Repo => Mazapan.Tests.Foundation.Repo.Root;

    const string Manifest = """
        {
          "version": "2.1.289",
          "platforms": {
            "darwin-arm64": { "binary": "claude", "checksum": "03d66745e3bb69ec727d66023696f3820bc0a00a8a5ba725eb6706d0c67cbe69", "size": 229616464 },
            "linux-x64": { "binary": "claude", "checksum": "a186b99e4a9c88366cd49df2f7dad56c61fc306ef0140b19ee64b7c42a8d1348", "size": 246107320 }
          }
        }
        """;

    [Fact]
    public void CodingAgentsAreAProfile()
    {
        var (profiles, apps) = AppCatalog.Load(Path.Join(Repo, "catalog", "apps.toml"));
        var agents = profiles.Single(p => p.Id == "agents");
        Assert.Contains("claude-code", agents.Apps);
        Assert.All(agents.Apps, id => Assert.Equal("agents", apps.Single(a => a.Id == id).Category));
        // Terminal programs opened through their command; T3 Code, a window, through its launcher.
        Assert.All(agents.Apps.Where(id => id != "t3code"), id => Assert.NotEqual("", apps.Single(a => a.Id == id).Command));
        Assert.True(Vendor.MakerOf(apps.Single(a => a.Id == "t3code").Vendor).HasLauncher);
        Assert.False(Vendor.MakerOf(apps.Single(a => a.Id == "herdr").Vendor).HasLauncher);
        var claude = apps.Single(a => a.Id == "claude-code");
        Assert.Equal("vendor", claude.Kind);
        Assert.True(Vendor.MakerOf(claude.Vendor).SelfInstalls);
    }

    const string HerdrRelease = """
        {
          "tag_name": "v0.9.3",
          "assets": [
            { "name": "herdr-linux-aarch64", "size": 27606640, "digest": "sha256:4de7aa3e25678812e92960de64f7c2aaa1bca1f0f80a3c5e559837e231e1f5c0",
              "browser_download_url": "https://github.com/herdrdev/herdr/releases/download/v0.9.3/herdr-linux-aarch64" },
            { "name": "herdr-linux-x86_64", "size": 29962088, "digest": "sha256:18a8dc65f1c2fa485884344356dea1cfd911c6f06cf46fa78e193f4087f4dba7",
              "browser_download_url": "https://github.com/herdrdev/herdr/releases/download/v0.9.3/herdr-linux-x86_64" }
          ]
        }
        """;

    [Fact]
    public void GitHubReleaseForThisMachine()
    {
        var herdr = Vendor.MakerOf("github:herdrdev/herdr");
        var r = Vendor.GitHubRelease(herdr, HerdrRelease, arm64: false);
        Assert.Equal("0.9.3", r.Version);
        Assert.Equal("https://github.com/herdrdev/herdr/releases/download/v0.9.3/herdr-linux-x86_64", r.Url);
        Assert.Equal("18a8dc65f1c2fa485884344356dea1cfd911c6f06cf46fa78e193f4087f4dba7", r.Sha256);
        Assert.Equal("herdr-linux-aarch64", Path.GetFileName(Vendor.GitHubRelease(herdr, HerdrRelease, arm64: true).Url));
        // T3 Code's AppImage, named with its version.
        var t3 = Vendor.MakerOf("github:pingdotgg/t3code");
        var t3json = """{ "tag_name": "v0.0.45", "assets": [ { "name": "T3-Code-0.0.45-x86_64.AppImage", "size": 1, "digest": "sha256:ab7b0a86d1ea657ccc162b60b772c61f70bc7c8b9e259b46939d53bb38faa02a", "browser_download_url": "https://github.com/pingdotgg/t3code/releases/download/v0.0.45/T3-Code-0.0.45-x86_64.AppImage" } ] }""";
        Assert.Equal("0.0.45", Vendor.GitHubRelease(t3, t3json, arm64: false).Version);
    }

    [Fact]
    public void GitHubReleaseIsChecked()
    {
        var herdr = Vendor.MakerOf("github:herdrdev/herdr");
        // No checksum kept, a file from elsewhere, no asset for the machine.
        Assert.Throws<MazapanException>(() => Vendor.GitHubRelease(herdr, HerdrRelease.Replace("\"digest\": \"sha256:18a8", "\"x\": \"sha256:18a8"), arm64: false));
        Assert.Throws<MazapanException>(() => Vendor.GitHubRelease(herdr, HerdrRelease.Replace("https://github.com/herdrdev/herdr/releases/download/v0.9.3/herdr-linux-x86_64", "https://evil.example/herdr"), arm64: false));
        Assert.Throws<MazapanException>(() => Vendor.GitHubRelease(herdr, HerdrRelease.Replace("herdr-linux-x86_64", "herdr-linux-riscv"), arm64: false));
    }

    [Fact]
    public void ClaudesReleaseFromItsManifest()
    {
        var r = Vendor.ClaudeRelease("2.1.289", Manifest, "linux-x64");
        Assert.Equal("2.1.289", r.Version);
        Assert.Equal("https://downloads.claude.ai/claude-code-releases/2.1.289/linux-x64/claude", r.Url);
        Assert.Equal("a186b99e4a9c88366cd49df2f7dad56c61fc306ef0140b19ee64b7c42a8d1348", r.Sha256);
        Assert.Equal(246107320, r.Size);
    }

    [Fact]
    public void ClaudesManifestIsChecked()
    {
        // No build for the platform, another version's manifest, a version that isn't one, a checksum that isn't one.
        Assert.Throws<MazapanException>(() => Vendor.ClaudeRelease("2.1.289", Manifest, "linux-arm64"));
        Assert.Throws<MazapanException>(() => Vendor.ClaudeRelease("2.1.290", Manifest, "linux-x64"));
        Assert.Throws<MazapanException>(() => Vendor.ClaudeRelease("../x", Manifest.Replace("2.1.289", "../x"), "linux-x64"));
        Assert.Throws<MazapanException>(() => Vendor.ClaudeRelease("2.1.289", Manifest.Replace("a186b99e", "zz6b99e4"), "linux-x64"));
    }

    [Fact]
    public void ClaudeInstalledByHandCounts()
    {
        var home = Path.Join(Path.GetTempPath(), "mazapan-vendor-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Equal("", Vendor.ClaudeInstalled(home));
            var versions = Path.Join(home, ".local/share/claude/versions");
            Directory.CreateDirectory(versions);
            File.WriteAllText(Path.Join(versions, "2.1.9"), "");
            File.WriteAllText(Path.Join(versions, "2.1.289"), "");
            File.WriteAllText(Path.Join(versions, "not-a-version"), "");
            Assert.Equal("2.1.289", Vendor.ClaudeInstalled(home));
            // Only its command (another way in): there, version unknown.
            Directory.Delete(Path.Join(home, ".local/share/claude"), true);
            Directory.CreateDirectory(Path.Join(home, ".local/bin"));
            File.WriteAllText(Path.Join(home, ".local/bin/claude"), "");
            Assert.Equal("installed", Vendor.ClaudeInstalled(home));
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, true);
        }
    }
}
