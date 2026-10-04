using Mazapan.Config;
using Mazapan.Hardware;
using Mazapan.Plugins;
using Mazapan.Tests.Foundation;
using Mazapan.Util;

namespace Mazapan.Tests.Hardware;

public class HardwareTests
{
    /// <summary>A /sys and /proc of a laptop with an NVIDIA and an AMD GPU, a Keychron, a Synaptics touchpad.</summary>
    static string FakeRoot(TempDir d)
    {
        d.Write("sys/class/dmi/id/sys_vendor", "LENOVO\n");
        d.Write("sys/class/dmi/id/product_name", "83HE\n");
        d.Write("sys/class/dmi/id/product_version", "Yoga Pro 7 14IAH10\n");
        void Pci(string addr, string v, string dev, string cls, string driver)
        {
            d.Write($"sys/bus/pci/devices/{addr}/vendor", v + "\n");
            d.Write($"sys/bus/pci/devices/{addr}/device", dev + "\n");
            d.Write($"sys/bus/pci/devices/{addr}/class", cls + "\n");
            d.Dir("drivers/" + driver);
            File.CreateSymbolicLink(Path.Join(d.Path, $"sys/bus/pci/devices/{addr}/driver"), Path.Join(d.Path, "drivers", driver));
        }
        Pci("0000:01:00.0", "0x10de", "0x2684", "0x030000", "nvidia");
        Pci("0000:0c:00.0", "0x1002", "0x13c0", "0x030000", "amdgpu");
        Pci("0000:00:00.0", "0x1022", "0x14d8", "0x060000", "");
        d.Write("sys/bus/usb/devices/1-1/idVendor", "3434\n");
        d.Write("sys/bus/usb/devices/1-1/idProduct", "0350\n");
        d.Write("sys/bus/usb/devices/1-1/product", "Keychron Q5\n");
        d.Write("proc/bus/input/devices", "I: Bus=0011\nN: Name=\"SynPS/2 Synaptics TouchPad\"\n\nN: Name=\"Keychron Q5\"\n");
        d.Write("proc/modules", "hid_apple 28672 0 - Live 0x0\nnvidia_drm 139264 3 - Live 0x0\n");
        d.Write("proc/mounts", "sysfs /sys sysfs rw 0 0\n/dev/nvme0n1p2 / btrfs rw,subvol=/@ 0 0\n/dev/nvme0n1p2 /home btrfs rw,subvol=/@home 0 0\n");
        d.Write("boot/limine.conf", "timeout: 3\n");
        d.Write("usr/share/hwdata/pci.ids", "10de  NVIDIA Corporation\n\t2684  AD102 [GeForce RTX 4090]\n1002  Advanced Micro Devices, Inc. [AMD/ATI]\n\t13c0  Granite Ridge [Radeon Graphics]\nC 00  Unclassified device\n");
        return d.Path;
    }

    [Fact]
    public void AMachineIsReadFromSys()
    {
        using var d = new TempDir();
        var m = Machine.Read(FakeRoot(d));
        Assert.Equal("LENOVO", m.Vendor);
        Assert.Equal("Yoga Pro 7 14IAH10", m.Version);
        Assert.Equal(2, m.Gpus.Count());
        Assert.Equal("NVIDIA Corporation AD102 [GeForce RTX 4090]", m.Gpus.First().Name);
        Assert.Equal("nvidia", m.Gpus.First().Driver);
        Assert.Contains(m.Usb, u => u.Vendor == "3434" && u.Name == "Keychron Q5");
        Assert.Contains("SynPS/2 Synaptics TouchPad", m.Inputs);
        Assert.Contains("hid_apple", m.Modules);
        Assert.Equal("btrfs", m.Filesystem);
        Assert.Equal("limine", m.Bootloader);
    }

    [Theory]
    [InlineData("gpus = 2", true)]
    [InlineData("gpus = 3", false)]
    [InlineData("gpu = [\"NVIDIA*AD1*\"]", true)]
    [InlineData("gpu = [\"*Radeon*\"]", true)]
    [InlineData("gpu = [\"NVIDIA*TU1*\"]", false)]
    [InlineData("pci = [\"10de:*\"]", true)]
    [InlineData("pci = [\"8086:*\"]", false)]
    [InlineData("usb = [\"3434:*\"]", true)]
    [InlineData("input = [\"*synaptics*\"]", true)]
    [InlineData("modules = [\"hid_apple\"]", true)]
    [InlineData("vendor = [\"lenovo\"]\nproduct = [\"*Yoga Pro 7 14IAH10*\"]", true)]
    [InlineData("vendor = [\"Apple*\"]\nmodules = [\"hid_apple\"]", false)]
    [InlineData("filesystem = [\"btrfs\"]", true)]
    [InlineData("filesystem = [\"ext4\"]", false)]
    [InlineData("filesystem = [\"btrfs\"]\nbootloader = [\"limine\"]", true)]
    [InlineData("filesystem = [\"btrfs\"]\nbootloader = [\"grub\"]", false)]
    [InlineData("filesystem = [\"btrfs\"]\nboot_on_root = true", true)]
    public void RulesMatch(string rules, bool want)
    {
        using var d = new TempDir();
        var m = Machine.Read(FakeRoot(d));
        d.Write("p/plugin.toml", $"[plugin]\nid = \"p\"\nversion = \"1.0\"\napi = 1\n\n[hardware]\n{rules}\n");
        var p = Plugin.Load(Path.Join(d.Path, "p"));
        Assert.Equal(want, p.Hardware!.Matches(m).Ok);
    }

    [Fact]
    public void ABootPartitionAndTheFirmwaresLoaderAreSeen()
    {
        using var d = new TempDir();
        FakeRoot(d);
        // /boot the EFI partition (unreadable to the user): the firmware says
        // which loader started; a snapshot of / wouldn't have the kernel.
        d.Write("proc/mounts", "/dev/nvme0n1p2 / btrfs rw,subvol=/@ 0 0\n/dev/nvme0n1p1 /boot vfat rw,fmask=0077,dmask=0077 0 0\n");
        File.Delete(Path.Join(d.Path, "boot/limine.conf"));
        var info = new byte[] { 6, 0, 0, 0 }.Concat(System.Text.Encoding.Unicode.GetBytes("systemd-boot 258\0")).ToArray();
        d.Dir("sys/firmware/efi/efivars");
        File.WriteAllBytes(Path.Join(d.Path, "sys/firmware/efi/efivars/LoaderInfo-4a67b082-0a4c-41cf-b6c7-440b29bb8c4f"), info);
        var m = Machine.Read(d.Path);
        Assert.False(m.BootOnRoot);
        Assert.Equal("systemd-boot", m.Bootloader);
        var rules = new Rules { Filesystem = ["btrfs"], BootOnRoot = true };
        Assert.False(rules.Matches(m).Ok);
        // The way it's installed is part of which machine it is.
        Assert.False(new Rules { Bootloader = ["grub"] }.MatchesIdentity(m));
    }

    static Plugin Manifest(TempDir d, string target)
    {
        d.Write("p/f.tmpl", "x");
        d.Write("p/plugin.toml", $"[plugin]\nid = \"p\"\nversion = \"1.0\"\napi = 1\n\n[hardware]\ngpus = 1\n\n[[targets]]\ntemplate = \"f.tmpl\"\n{target}\n");
        return Plugin.Load(Path.Join(d.Path, "p"));
    }

    [Theory]
    [InlineData("output = \"/etc/modprobe.d/mazapan-x.conf\"\nsystem = true", true)]
    [InlineData("output = \"/etc/pacman.d/hooks/mazapan-x.hook\"\nsystem = true", null)]
    [InlineData("output = \"/etc/systemd/system/sshd.service.d/mazapan-x.conf\"\nsystem = true", false)]
    [InlineData("output = \"/etc/passwd\"\nsystem = true", false)]
    [InlineData("output = \"/etc/modprobe.d/x.conf\"\nsystem = true", false)]
    [InlineData("output = \"/etc/modprobe.d/mazapan-x.conf\"", false)]
    [InlineData("output = \"~/x\"\nreboot = true", false)]
    public void SystemFilesAreMazapansDropIns(string target, bool? ok)
    {
        using var d = new TempDir();
        if (ok == null) Assert.NotNull(Manifest(d, target)); // allowed, elsewhere than modprobe.d
        else if (ok == true) Assert.Contains("full access, as root: writes /etc/modprobe.d/mazapan-x.conf", Manifest(d, target).Capabilities());
        else Assert.Throws<MazapanException>(() => Manifest(d, target));
    }

    [Fact]
    public void AHardwarePluginIsOffUntilTurnedOn()
    {
        using var d = new TempDir();
        var p = Manifest(d, "output = \"~/x\"");
        var cfg = new Settings();
        Assert.False(cfg.IsOn(p));
        cfg.TurnOn(p);
        Assert.True(cfg.IsOn(p));
        Assert.Contains("p", cfg.Enabled);
        cfg.TurnOff(p);
        Assert.False(cfg.IsOn(p));
        Assert.DoesNotContain("p", cfg.Disabled);
    }

    [Fact]
    public void BuiltInHardwarePluginsLoad()
    {
        var (all, broken) = Plugin.Discover([Path.Join(Repo.Root, "plugins")]);
        Assert.True(broken.Count == 0, string.Join("\n", broken.Select(b => b.Key + ": " + b.Value)));
        var hw = all.Where(p => p.Hardware != null).Select(p => p.Id).ToList();
        foreach (var id in new[] { "hw-multi-gpu-cursor", "hw-apple-fnkeys", "hw-nvidia", "hw-intel-video", "hw-synaptics-intertouch", "hw-yoga-pro7-bass" })
            Assert.Contains(id, hw);
    }

    // The installer turns on this machine's hardware plugins and installs
    // their packages from the ISO's own repository, offline: each must be in
    // what the ISO carries, or an install on that machine stops asking for it.
    [Fact]
    public void WhatTheInstallerTurnsOnTheIsoCarries()
    {
        var carried = File.ReadAllLines(Path.Join(Repo.Root, "iso", "target-packages.txt"))
            .Select(l => l.Split('#')[0].Trim()).Where(l => l != "").ToHashSet();
        var (all, broken) = Plugin.Discover([Path.Join(Repo.Root, "plugins")]);
        Assert.Empty(broken);
        var missing = all.Where(p => p.Hardware is { } h && !h.OnlyAny && !h.Live)
            .SelectMany(p => p.Pacman.Where(x => !carried.Contains(x)).Select(x => $"{p.Id}: {x}")).ToList();
        Assert.Empty(missing);
    }
}

