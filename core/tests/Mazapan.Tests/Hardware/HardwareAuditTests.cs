using Mazapan.Applying;
using Mazapan.Hardware;
using Mazapan.Plugins;
using Mazapan.Rendering;
using Mazapan.Tests.Foundation;
using Mazapan.Themes;
using Mazapan.Util;

namespace Mazapan.Tests.Hardware;

/// <summary>What the audit of hardware plugins and --system found, kept from coming back.</summary>
public class HardwareAuditTests
{
    [Theory]
    [InlineData("/etc/modprobe.d/mazapan-nvidia.conf", true)]
    [InlineData("/etc/modprobe.d/mazapan-x.conf.mazapan-bak-20260929", true)]
    [InlineData("/etc/pacman.conf", false)]
    [InlineData("/etc/sudoers.d/mazapan-x", false)]
    [InlineData("/etc/modprobe.d/nvidia.conf", false)]
    [InlineData("/etc/modprobe.d/../pacman.conf", false)]
    [InlineData("/etc/modprobe.d//mazapan-x.conf", false)]
    [InlineData("/etc/pam.d/polkit-1", true)]          // the one override of a /usr/lib file
    [InlineData("/etc/pam.d/sudo", false)]
    [InlineData("/etc/pam.d/system-auth", false)]
    [InlineData("/etc/pam.d/polkit-1.d", false)]
    [InlineData("etc/modprobe.d/mazapan-x.conf", false)]
    public void OnlyMazapanDropInsAreWrittenAsRoot(string path, bool ok) => Assert.Equal(ok, AsRoot.IsSystem(path));

    [Fact]
    public void AHomeFileIsNeverASystemFile() => Assert.False(AsRoot.IsSystem(Path.Join(Paths.Home, ".config", "x")));

    [Theory]
    [InlineData("linux", true)]
    [InlineData("lib32-nvidia-utils", true)]
    [InlineData("--config=/tmp/evil.conf", false)]
    [InlineData("-Rns", false)]
    [InlineData("", false)]
    public void PackageNamesAreNeverOptions(string name, bool ok) => Assert.Equal(ok, AsRoot.IsPackage(name));

    static Plugin Load(TempDir d, string body)
    {
        d.Write("p/f.tmpl", "x");
        d.Write("p/plugin.toml", "[plugin]\nid = \"p\"\nversion = \"1.0\"\napi = 1\n\n" + body);
        return Plugin.Load(Path.Join(d.Path, "p"));
    }

    [Fact]
    public void AnythingWrittenAsRootTakesNoText()
    {
        using var d = new TempDir();
        var e = Assert.Throws<MazapanException>(() => Load(d, "[settings]\nopt = \"x\"\n\n[hardware]\ngpus = 1\n\n[[targets]]\ntemplate = \"f.tmpl\"\noutput = \"/etc/modprobe.d/mazapan-x.conf\"\nsystem = true\n"));
        Assert.Contains("only number and true/false settings", e.Message);
    }

    [Fact]
    public void AManifestsPackagesAreNames()
    {
        using var d = new TempDir();
        Assert.Throws<MazapanException>(() => Load(d, "[packages]\npacman = [\"--overwrite=*\"]\n"));
    }

    [Theory]
    [InlineData("gpu = [\"*[GeForce RTX 40*\"]", false)]
    [InlineData("gpu = [\"*\\\\[GeForce RTX 40*\"]", true)]
    public void APatternThatCantMatchIsAnError(string rule, bool ok)
    {
        using var d = new TempDir();
        if (ok) Load(d, "[hardware]\n" + rule + "\n");
        else Assert.Throws<MazapanException>(() => Load(d, "[hardware]\n" + rule + "\n"));
    }

    [Fact]
    public void NothingRequiresAHardwarePlugin()
    {
        var hw = new Plugin();
        hw.Meta.Id = "hw-x";
        hw.Meta.Version = "1.0";
        hw.Hardware = new Rules { Gpus = 1 };
        var p = new Plugin();
        p.Meta.Id = "p";
        p.Meta.Version = "1.0";
        p.Meta.Requires = ["hw-x"];
        Assert.Contains("a hardware plugin", Dependencies.Unmet([p, hw], [p, hw], null).Single().Text);
    }

    [Fact]
    public void WhatComesAndGoesOnlyDecidesTheOffer()
    {
        var m = new Machine { Vendor = "LENOVO", Product = "X1" };
        var r = new Rules { Vendor = ["lenovo"], Modules = ["hid_apple"] };
        Assert.False(r.Matches(m).Ok); // the keyboard isn't plugged in: not offered
        Assert.True(r.MatchesIdentity(m)); // but if on, it stays on this machine
        Assert.False(new Rules { Vendor = ["dell"] }.MatchesIdentity(m));
    }

    [Fact]
    public void TemplatesSeeTheMachine()
    {
        using var d = new TempDir();
        d.Write("f.tmpl", "{{ machine.gpus.size >= 0 }} {{ machine.vendor != null }}");
        var p = new Plugin { Dir = d.Path };
        p.Meta.Id = "p";
        p.Targets.Add(new Target { Template = "f.tmpl", Output = Path.Join(d.Path, "o") });
        var t = new Theme { Id = "t" };
        t.Motion.ResponseMS = 400;
        t.Motion.Damping = 0.8;
        Assert.Equal("true true", Renderer.All([p], t, _ => null, "en").Files[0].Content);
    }
}
