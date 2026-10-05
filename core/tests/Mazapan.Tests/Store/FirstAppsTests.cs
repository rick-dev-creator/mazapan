using Mazapan.Setup;
using Mazapan.Store;

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
}
