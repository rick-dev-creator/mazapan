using Mazapan.Tests.Applying;
using C = Mazapan.Checkpoints.Checkpoints;
using Checkpoint = Mazapan.Checkpoints.Checkpoint;

namespace Mazapan.Tests.Checkpoints;

public sealed class CheckpointsTests
{
    [Theory]
    [InlineData("BOOT_IMAGE=/vmlinuz-linux root=UUID=x rw rootflags=subvol=@", null)]
    [InlineData("root=UUID=x rw rootflags=subvol=@/.snapshots/12/snapshot systemd.volatile=overlay mazapan.checkpoint=12", "12")]
    [InlineData("root=UUID=x rw rootflags=subvol=@-previous-20261004-120000 mazapan.checkpoint=previous-20261004-120000", "previous-20261004-120000")]
    // grub-btrfs's own entries: a snapshot mounted as root, quoted or not.
    [InlineData("root=UUID=x rw rootflags=subvol=\"@/.snapshots/7/snapshot\"", "7")]
    [InlineData("root=UUID=x rw rootflags=compress=zstd,subvol=/.snapshots/3/snapshot", "3")]
    public void Booted_ReadsTheKernelsLine(string cmdline, string? want) => Assert.Equal(want, C.Booted(cmdline));

    [Theory]
    [InlineData("pacman -Syu", "linux mesa systemd", "update", "before an update (3 packages)")]
    [InlineData("/usr/bin/pacman -S --noconfirm --needed firefox", "firefox", "install", "before installing firefox")]
    [InlineData("pacman -S a b c d e", "", "install", "before installing a, b, c and 2 more")]
    [InlineData("pacman -Rns gimp", "gimp gegl", "remove", "before removing gimp, gegl")]
    [InlineData("pacman -U /tmp/x/foo-1-1-x86_64.pkg.tar.zst", "", "install", "before installing foo-1-1-x86_64.pkg.tar.zst")]
    [InlineData("mazapan installed", "", "installed", "the system as installed")]
    [InlineData("by hand", "", "other", "before by hand")]
    public void Describe_SaysWhatItWasBefore(string pre, string post, string kind, string title)
    {
        var (k, p) = C.Describe(pre, post);
        Assert.Equal(kind, k);
        Assert.Equal(title, C.Title(k, p, pre));
    }

    const string Cfg = """
        ### BEGIN /etc/grub.d/10_linux ###
        menuentry 'Arch Linux' --class arch --class gnu-linux --class gnu --class os $menuentry_id_option 'gnulinux-simple-ab' {
        	load_video
        	set gfxpayload=keep
        	insmod gzio
        	insmod part_gpt
        	insmod fat
        	search --no-floppy --fs-uuid --set=root 1234-ABCD
        	echo	'Loading Linux linux ...'
        	linux	/vmlinuz-linux root=UUID=ab rw rootflags=subvol=@ rd.luks.name=cd=root resume=UUID=ab resume_offset=99 loglevel=3 quiet splash
        	echo	'Loading initial ramdisk ...'
        	initrd	/intel-ucode.img /initramfs-linux.img
        }
        submenu 'Advanced options for Arch Linux' $menuentry_id_option 'gnulinux-advanced-ab' {
        """;

    [Fact]
    public void MainEntry_IsGrubsFirstMenuentry()
    {
        var e = C.MainEntry(Cfg)!;
        Assert.Equal("/vmlinuz-linux", e.Kernel);
        Assert.Equal(["/intel-ucode.img", "/initramfs-linux.img"], e.Initrds);
        Assert.Contains("rootflags=subvol=@", e.Args);
        Assert.Null(C.MainEntry("set default=0\n"));
    }

    [Fact]
    public void Args_StartTheCheckpointOnAnOverlay()
    {
        var a = C.Args("root=UUID=ab rw rootflags=compress=zstd,subvol=@ resume=UUID=ab resume_offset=99 quiet", "@/.snapshots/12/snapshot", "12");
        Assert.Equal("root=UUID=ab rw rootflags=subvol=@/.snapshots/12/snapshot,compress=zstd quiet systemd.volatile=overlay systemd.mask=systemd-remount-fs.service mazapan.checkpoint=12", a);
        // No rootflags on the main line: added.
        Assert.Equal("root=UUID=ab rw rootflags=subvol=@-previous-x systemd.volatile=overlay systemd.mask=systemd-remount-fs.service mazapan.checkpoint=previous-x",
            C.Args("root=UUID=ab rw", "@-previous-x", "previous-x"));
    }

    [Fact]
    public void Entry_KeepsTheMainEntrysLines()
    {
        var e = C.MainEntry(Cfg)!;
        var s = C.Entry(e, "Oct 4, 14:02: before an update (it's 3)", "/mazapan/k/aa", ["/mazapan/k/bb", "/mazapan/k/cc"], "root=UUID=ab mazapan.checkpoint=12");
        Assert.StartsWith("  menuentry 'Oct 4, 14:02: before an update (it'\\''s 3)' --class arch {\n", s);
        Assert.Contains("    search --no-floppy --fs-uuid --set=root 1234-ABCD\n", s);
        Assert.Contains("    linux /mazapan/k/aa root=UUID=ab mazapan.checkpoint=12\n", s);
        Assert.Contains("    initrd /mazapan/k/bb /mazapan/k/cc\n", s);
        Assert.DoesNotContain("echo", s);
        Assert.EndsWith("  }\n", s);
    }

    [Theory]
    [InlineData("/@/boot/vmlinuz-linux", "/@/.snapshots/12/snapshot/boot/vmlinuz-linux")]
    [InlineData("/vmlinuz-linux", "/vmlinuz-linux")]
    [InlineData("/@home/x", "/@home/x")]
    public void InSubvol_MovesPathsOnTheRootFilesystem(string path, string want) =>
        Assert.Equal(want, C.InSubvol(path, "@", "@/.snapshots/12/snapshot"));

    [Fact]
    public void Kept_CheckpointsGetTheKernelsOfTheirTime()
    {
        var since = DateTimeOffset.Parse("2026-10-04T10:00:00Z");
        var k = new C.Kept { CurrentSince = since, Current = new() { ["/vmlinuz-linux"] = "k/aa" } };
        k.Sets["3"] = new() { ["/vmlinuz-linux"] = "k/old" };
        Checkpoint At(string id, string when) => new(id, long.Parse(id), DateTimeOffset.Parse(when), "update", [], "");
        k.Assign([At("5", "2026-10-04T11:00:00Z"), At("4", "2026-10-04T09:00:00Z"), At("3", "2026-10-04T12:00:00Z")]);
        Assert.Equal("k/aa", k.Sets["5"]["/vmlinuz-linux"]);
        Assert.False(k.Sets.ContainsKey("4")); // before /boot was seen like this
        Assert.Equal("k/old", k.Sets["3"]["/vmlinuz-linux"]); // already had its own
        var back = C.Kept.Parse(k.ToJson());
        Assert.Equal(since, back.CurrentSince);
        Assert.Equal("k/aa", back.Current["/vmlinuz-linux"]);
        Assert.Equal("k/aa", back.Sets["5"]["/vmlinuz-linux"]);
        Assert.Empty(C.Kept.Parse("not json").Sets);
    }

    [Fact]
    public void Packages_AndWhatDiffers()
    {
        using var a = new TempDir();
        using var b = new TempDir();
        foreach (var d in new[] { "linux-6.17.1.arch1-1", "mesa-1:25.1.0-2", "lib32-foo-bar-2.0-1", "gone-1-1" })
            Directory.CreateDirectory(Path.Join(a.Path, "var/lib/pacman/local", d));
        foreach (var d in new[] { "linux-6.17.2.arch1-1", "mesa-1:25.1.0-2", "lib32-foo-bar-2.0-1", "new-3-1" })
            Directory.CreateDirectory(Path.Join(b.Path, "var/lib/pacman/local", d));
        var pa = C.Packages(a.Path);
        Assert.Equal("1:25.1.0-2", pa["mesa"]);
        Assert.Equal("2.0-1", pa["lib32-foo-bar"]);
        Assert.Equal(
            [("gone", "1-1", ""), ("linux", "6.17.1.arch1-1", "6.17.2.arch1-1"), ("new", "", "3-1")],
            C.Diff(pa, C.Packages(b.Path)));
    }
}
