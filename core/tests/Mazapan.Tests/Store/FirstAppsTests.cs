using Mazapan.Setup;
using Mazapan.Store;
using Mazapan.Tests.Foundation;

namespace Mazapan.Tests.Store;

public class FirstAppsTests
{
    [Fact]
    public void FirstAppsReadsIdsOnly()
    {
        // An older install marked it "#started" before trying: still asked for.
        Assert.Equal(["chromium", "steam", "dotnet"], FirstApps.Parse("#started\nchromium\nsteam\n\nBad Id\nsteam\ndotnet\n../x\n"));
        Assert.Empty(FirstApps.Parse(""));
    }

    [Fact]
    public void PacmanTargetsNotFound()
    {
        var t = PacmanTrouble.Read("resolving dependencies...\nerror: target not found: zed\nerror: target not found: protonplus\n");
        Assert.Equal(new HashSet<string> { "zed", "protonplus" }, t.Missing);
        Assert.False(t.Keyring);
        Assert.False(t.NoAgent);
    }

    [Theory]
    [InlineData("error: steam: signature from \"Someone <a@b>\" is unknown trust")]
    [InlineData("error: failed to commit transaction (invalid or corrupted package (PGP signature))")]
    [InlineData("error: key \"ABCDEF\" is unknown")]
    [InlineData("error: required key missing from keyring")]
    public void PacmanKeyringTrouble(string said) => Assert.True(PacmanTrouble.Read(said).Keyring);

    [Fact]
    public void PkexecWithoutAgent()
    {
        var t = PacmanTrouble.Read("Error executing command as another user: No authentication agent found.\n");
        Assert.True(t.NoAgent);
        Assert.Empty(t.Missing);
        Assert.False(PacmanTrouble.Read("Error executing command as another user: Not authorized\n").NoAgent);
    }

    const string Wifi = """
        [connection]
        id=Home
        uuid=1f0e6a7e-0000-4000-8000-000000000001
        type=wifi
        permissions=user:live;
        interface-name=wlan0

        [wifi]
        mode=infrastructure
        ssid=Home

        [wifi-security]
        key-mgmt=wpa-psk
        psk=secret123

        [ipv4]
        method=auto
        """;

    [Fact]
    public void WifiCarriedWithoutTheLiveUser()
    {
        var k = WifiCarry.Keyfile(Wifi)!;
        Assert.DoesNotContain("permissions=", k);
        // The live system's name for the card, not the installed one's.
        Assert.DoesNotContain("interface-name=", k);
        Assert.Contains("psk=secret123", k);
        Assert.Contains("type=wifi", k);
    }

    [Fact]
    public void WifiNotCarried()
    {
        // A cable's, a password kept by an agent, a secured one without its password.
        Assert.Null(WifiCarry.Keyfile(Wifi.Replace("type=wifi", "type=ethernet")));
        Assert.Null(WifiCarry.Keyfile(Wifi.Replace("psk=secret123", "psk-flags=1")));
        Assert.Null(WifiCarry.Keyfile(Wifi.Replace("psk=secret123\n", "")));
        // Open: no [wifi-security] at all, carried.
        Assert.NotNull(WifiCarry.Keyfile(Wifi[..Wifi.IndexOf("[wifi-security]")] + "[ipv4]\nmethod=auto\n"));
    }

    static List<App> Catalog() => AppCatalog.Load(Path.Join(Repo.Root, "catalog", "apps.toml")).Apps;

    // Basic, chosen in the installer, goes in with the system from the ISO's
    // own repository: every package of it must be there.
    [Fact]
    public void TheIsoCarriesTheBasicProfile()
    {
        var carried = File.ReadAllLines(Path.Join(Repo.Root, "iso", "target-packages.txt"))
            .Select(l => l.Split('#')[0].Trim()).Where(l => l != "").ToHashSet();
        var (profiles, apps) = AppCatalog.Load(Path.Join(Repo.Root, "catalog", "apps.toml"));
        var basic = profiles.Single(p => p.Basic);
        var missing = basic.Apps.Select(id => apps.Single(a => a.Id == id)).SelectMany(a => a.Pacman).Where(p => !carried.Contains(p)).ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void AppPackagesFromTheIsoFirst()
    {
        // Chromium is on the ISO, Steam isn't; Heroic is Flathub's (the first login's).
        var (offline, online) = Archinstall.AppPackages(["chromium", "steam", "heroic", "nope"], Catalog(), new HashSet<string> { "chromium" });
        Assert.Equal(["chromium"], offline);
        Assert.Contains("steam", online);
        Assert.DoesNotContain("chromium", online);
        Assert.DoesNotContain(online.Concat(offline), p => p.Contains("heroic", StringComparison.OrdinalIgnoreCase));
        // Nothing offline: all of it waits for a connection.
        var (none, all) = Archinstall.AppPackages(["chromium"], Catalog(), new HashSet<string>());
        Assert.Empty(none);
        Assert.Equal(["chromium"], all);
    }

    [Fact]
    public void AppsGoInWithTheSystem()
    {
        var a = Answers.Parse("""
            {"disk":"/dev/vda","user":"rick","password":"p4ssw0rd","timezone":"UTC","language":"en","theme":"phosphor","apps":["chromium","steam"]}
            """);
        a.OfflineAppPackages = ["chromium", "git"];
        a.AppPackages = ["steam", "lib32-gamemode"];
        using var doc = System.Text.Json.JsonDocument.Parse(Archinstall.Config(a, 512110190592, "us"));
        var packages = doc.RootElement.GetProperty("packages").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("chromium", packages);
        Assert.Single(packages, p => p == "git");
        var post = Archinstall.Post(a);
        Assert.Contains("pacman -T -- steam lib32-gamemode", post);
        Assert.Contains("pacman -S --needed --noconfirm -- $apps", post);
        // After the first update (the repositories synced), written down for the Apps menu; git is the system's.
        Assert.True(post.IndexOf("pacman -Syu", StringComparison.Ordinal) < post.IndexOf("pacman -S --needed", StringComparison.Ordinal));
        Assert.Contains("for p in chromium $apps;", post);
        Assert.Contains("/home/rick/.local/state/mazapan/first-packages", post);
        Assert.True(post.IndexOf("first-packages", StringComparison.Ordinal) < post.IndexOf("chown -R", StringComparison.Ordinal));
        // No apps: nothing of it.
        a.OfflineAppPackages = [];
        a.AppPackages = [];
        Assert.DoesNotContain("first-packages", Archinstall.Post(a));
    }

    [Fact]
    public void WhatTheInstallPutInIsTheAppsMenus()
    {
        var apps = Catalog().Where(a => a.Id is "chromium" or "steam" or "heroic").ToList();
        var installed = new HashSet<string> { "chromium", "steam", "base" };
        var tx = FirstApps.Adopted(apps, installed, "chromium\nsteam\ngone\nBad Name!\n", "en")!;
        Assert.Equal("install", tx.Action);
        Assert.Equal(["chromium", "steam"], tx.Packages);
        Assert.Contains("chromium", tx.Apps);
        Assert.DoesNotContain("heroic", tx.Apps);
        Assert.Null(FirstApps.Adopted(apps, installed, "gone\n", "en"));
    }
}
