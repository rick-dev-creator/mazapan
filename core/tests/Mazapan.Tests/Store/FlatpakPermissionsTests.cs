using Mazapan.Cli;
using Mazapan.Util;

namespace Mazapan.Tests.Store;

public class FlatpakPermissionsTests
{
    [Fact]
    public void WhatAnAppMayReachIsReadAsFlatpakSaysIt()
    {
        var info = "[Context]\nshared=network;ipc;\nsockets=x11;wayland;pulseaudio;\ndevices=dri;\nfilesystems=xdg-download:rw;!home;host:ro;\n\n[Session Bus Policy]\norg.freedesktop.Notifications=talk\nfilesystems=home\n";
        var p = FlatpakPermissions.Parse(info);
        Assert.True(p["network"]);
        Assert.True(p["sound"]);
        Assert.False(p["devices"]); // dri isn't all
        Assert.True(p["downloads"]);
        Assert.True(p["files"]);
        Assert.False(p["home"]);    // taken away, and the other group's doesn't count
        Assert.False(p["bluetooth"]);
    }

    [Fact]
    public void ASwitchIsTheUsersOwnOverride()
    {
        Assert.Equal(["override", "--user", "--unshare=network", "org.example.App"], FlatpakPermissions.Override("org.example.App", "network", false));
        Assert.Equal(["override", "--user", "--filesystem=home", "org.example.App"], FlatpakPermissions.Override("org.example.App", "home", true));
        Assert.Throws<MazapanException>(() => FlatpakPermissions.Override("org.example.App", "--filesystem=/", true));
    }

    [Fact]
    public void WhatItAskedForThroughAPortalIsItsOwnRows()
    {
        var shown = "devices\tcamera\tio.mpv.Mpv\tyes\t0x00\nlocation\tlocation\tio.mpv.Mpv\tno\t0x00\ndevices\tcamera\torg.other.App\tyes\t0x00\n--evil\tx\tio.mpv.Mpv\tyes\t\n";
        var g = FlatpakPermissions.ParseGrants(shown, "io.mpv.Mpv");
        Assert.Equal(["devices/camera", "location/location"], g.Select(x => x.Key));
        Assert.True(g[0].Allowed);
        Assert.False(g[1].Allowed);
        Assert.Equal(["permission-remove", "devices", "camera", "io.mpv.Mpv"], FlatpakPermissions.Portal("io.mpv.Mpv", "devices/camera", "ask"));
        Assert.Equal(["permission-set", "location", "location", "io.mpv.Mpv", "no"], FlatpakPermissions.Portal("io.mpv.Mpv", "location/location", "off"));
        Assert.Throws<MazapanException>(() => FlatpakPermissions.Portal("io.mpv.Mpv", "--x/y", "on"));
    }
}
