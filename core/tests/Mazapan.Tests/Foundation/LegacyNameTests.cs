using Mazapan.Applying;
using Mazapan.Tests.Install;
using Mazapan.Util;

namespace Mazapan.Tests.Foundation;

[Collection("InstallHome")]
public class LegacyNameTests
{
    [Fact]
    public void MyarchsFoldersBecomeMazapans()
    {
        using var s = new Sandbox();
        string H(string p) => Path.Join(s.Home, p);
        Directory.CreateDirectory(H(".config/myarch"));
        File.WriteAllText(H(".config/myarch/config.toml"), "theme = \"paper\"\n");
        Directory.CreateDirectory(H(".local/share/myarch/bin"));
        File.WriteAllText(H(".local/share/myarch/bin/idle"), "#!/bin/sh\n");
        Directory.CreateDirectory(H(".local/state/myarch-modes"));
        Directory.CreateDirectory(H(".local/share/applications"));
        File.WriteAllText(H(".local/share/applications/myarch-webapp-abc.desktop"),
            $"Exec=sh \"{H(".local/share/myarch/bin/webapp")}\" open abc\nX-MyArch-WebApp=https://a.com\nIcon=~/.local/share/myarch-webapps/abc.png\n");
        Directory.CreateDirectory(H(".config/myarch-notes"));   // the person's own
        Directory.CreateDirectory(H(".local/state/myarch"));
        var owned = new Owned
        {
            [H(".local/share/myarch/bin/idle")] = "1",
            [H(".config/quickshell/myarch/shell.qml")] = "2",   // generated where it is: an orphan next apply
            ["/etc/modprobe.d/myarch-x.conf"] = "3",
        };
        owned.Save(H(".local/state/myarch/owned.json"));

        LegacyName.Migrate();

        Assert.True(File.Exists(H(".config/mazapan/config.toml")));
        Assert.False(Directory.Exists(H(".config/myarch")));
        Assert.True(File.Exists(H(".local/share/mazapan/bin/idle")));
        Assert.True(Directory.Exists(H(".local/state/mazapan-modes")));
        var desktop = File.ReadAllText(H(".local/share/applications/mazapan-webapp-abc.desktop"));
        Assert.Contains("X-Mazapan-WebApp=https://a.com", desktop);
        Assert.Contains("mazapan-webapps", desktop);
        Assert.Contains($"Exec=sh \"{H(".local/share/mazapan/bin/webapp")}\" open abc", desktop);
        Assert.True(Directory.Exists(H(".config/myarch-notes")));
        var now = Apply.LoadOwned(Apply.StatePath());
        Assert.Equal("1", now.Get(H(".local/share/mazapan/bin/idle")));
        Assert.Equal("2", now.Get(H(".config/quickshell/myarch/shell.qml")));
        Assert.Equal("3", now.Get("/etc/modprobe.d/myarch-x.conf"));
        // What myarch wrote as root can still be taken away.
        Assert.True(AsRoot.IsSystem("/etc/modprobe.d/myarch-x.conf"));

        // Once: a second run changes nothing.
        LegacyName.Migrate();
        Assert.True(File.Exists(H(".config/mazapan/config.toml")));
    }

    [Fact]
    public void StoppedHalfwayItCarriesOn()
    {
        using var s = new Sandbox();
        string H(string p) => Path.Join(s.Home, p);
        // Killed after moving ~/.local/share/myarch and owned.json, before the rest.
        Directory.CreateDirectory(H(".config/myarch"));
        Directory.CreateDirectory(H(".local/share/mazapan/bin"));
        File.WriteAllText(H(".local/share/mazapan/bin/idle"), "#!/bin/sh\n");
        Directory.CreateDirectory(H(".local/state/mazapan"));
        new Owned { [H(".local/share/myarch/bin/idle")] = "1" }.Save(H(".local/state/mazapan/owned.json"));
        Directory.CreateDirectory(H(".local/state/myarch-modes"));

        LegacyName.Migrate();

        Assert.True(Directory.Exists(H(".config/mazapan")));
        Assert.True(Directory.Exists(H(".local/state/mazapan-modes")));
        Assert.Equal("1", Apply.LoadOwned(Apply.StatePath()).Get(H(".local/share/mazapan/bin/idle")));
        Assert.False(File.Exists(H(".config/.mazapan-rename.lock")));
    }

    [Fact]
    public void RemovedFilesTakeTheirEmptyFoldersWithThem()
    {
        using var s = new Sandbox();
        string H(string p) => Path.Join(s.Home, p);
        var widget = H(".config/quickshell/myarch/widgets/right/X.qml");
        var foot = H(".config/foot/foot.ini");
        foreach (var f in new[] { widget, foot })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(f)!);
            File.WriteAllText(f, "gen");
        }
        var owned = new Owned { [widget] = Apply.Sum("gen"), [foot] = Apply.Sum("gen") };
        var (ch, orphans) = Apply.Plan([], owned);
        Apply.Execute(ch, orphans, owned, false);
        Assert.False(Directory.Exists(H(".config/quickshell/myarch")));
        Assert.True(Directory.Exists(H(".config/quickshell")));   // not ours: stays
        Assert.True(Directory.Exists(H(".config/foot")));         // an app's: stays, even empty
    }
}

public class LegacyPathTests
{
    [Theory]
    [InlineData("~/.config/hypr/myarch/gaps.lua", "~/.config/hypr/mazapan/gaps.lua")]
    [InlineData("~/.config/quickshell/myarch/widgets/right/x.qml", "~/.config/quickshell/mazapan/widgets/right/x.qml")]
    [InlineData("sh ~/.local/share/myarch/bin/shell-reload", "sh ~/.local/share/mazapan/bin/shell-reload")]
    [InlineData("/etc/modprobe.d/myarch-x.conf", "/etc/modprobe.d/mazapan-x.conf")]
    [InlineData("~/.config/myarch-notes/x", "~/.config/myarch-notes/x")]   // not one of Mazapán's places
    [InlineData("~/.config/foot/foot.ini", "~/.config/foot/foot.ini")]
    [InlineData("hyprctl reload >/dev/null", "hyprctl reload >/dev/null")]
    [InlineData("full access, code in Hyprland (Lua): ~/.config/hypr/myarch/g.lua", "full access, code in Hyprland (Lua): ~/.config/hypr/mazapan/g.lua")]
    [InlineData("as root: /etc/modprobe.d/myarch-x.conf", "as root: /etc/modprobe.d/mazapan-x.conf")]
    [InlineData("/etc/myarch.conf", "/etc/myarch.conf")]   // only in the drop-in folders
    public void OldPlacesAreTheNewOnes(string old, string now) => Assert.Equal(now, LegacyName.Path(old));

    [Fact]
    public void WhatMyarchWroteIsntPutBack()
    {
        Assert.Throws<MazapanException>(() => LegacyName.RefuseOld(["~/.config/hypr/myarch/theme.lua"], "x"));
        LegacyName.RefuseOld(["~/.config/hypr/mazapan/theme.lua", "~/.config/foot/foot.ini"], "x");
    }
}

public class IconTests
{
    [Fact]
    public void TheShellsIconIsTheIcon() =>
        Assert.Equal(File.ReadAllText(Path.Join(Repo.Root, "assets", "mazapan.svg")),
            File.ReadAllText(Path.Join(Repo.Root, "plugins", "shell-bar", "mazapan.svg.tmpl")));
}
