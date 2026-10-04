using System.Text;
using System.Text.Json;
using Mazapan.Setup;
using Mazapan.Util;

namespace Mazapan.Tests.Setup;

public class SetupTests
{
    const string Good = """
        {"disk":"/dev/vda","encrypt":false,"user":"rick","fullname":"Rick","password":"p4ss 'x\" $y",
         "timezone":"America/Mexico_City","language":"es","locale":"es_MX.UTF-8","kb_layout":"latam","kb_variant":"",
         "theme":"phosphor","apps":["vscode","steam"]}
        """;

    internal static string WithKey(string key, string json) => With(key, json);
    static string With(string key, string json) =>
        Good.Replace($"\"{key}\":", $"\"{key}_old\":").TrimEnd().TrimEnd('}') + $",\"{key}\":{json}}}";

    [Fact]
    public void AnswersRead()
    {
        var a = Answers.Parse(Good);
        Assert.Equal("/dev/vda", a.Disk);
        Assert.Equal("rick-pc", a.Hostname); // from the user when not given
        Assert.Equal(["vscode", "steam"], a.Apps);
        Assert.False(a.Encrypt);
    }

    [Theory]
    [InlineData("disk", "\"/dev/../etc/passwd\"")]
    [InlineData("disk", "\"vda\"")]
    [InlineData("user", "\"Rick\"")]
    [InlineData("user", "\"root\"")]
    [InlineData("user", "\"rick\\n\"")]
    [InlineData("user", "\"a;rm -rf /\"")]
    [InlineData("fullname", "\"a:b\"")]
    [InlineData("password", "\"\"")]
    [InlineData("password", "\"a\\nb\"")]
    [InlineData("hostname", "\"-pc\"")]
    [InlineData("timezone", "\"../../etc/shadow\"")]
    [InlineData("locale", "\"es_MX\"")]
    [InlineData("kb_layout", "\"us;x\"")]
    [InlineData("kb_variant", "\"a b\"")]
    [InlineData("theme", "\"../x\"")]
    [InlineData("apps", "[1]")]
    [InlineData("user", "\"greeter\"")]
    [InlineData("apps", "[\"--x\"]")]
    [InlineData("user", "1")]
    public void WhatWouldReachTheNewSystemIsChecked(string key, string json) =>
        Assert.Throws<MazapanException>(() => Answers.Parse(With(key, json)));

    const string Lsblk = """
        {"blockdevices":[
          {"path":"/dev/sda","type":"disk","size":512110190592,"model":"Samsung SSD 870 ","tran":"sata","rm":false,"ro":false,"fstype":null,"label":null,"partlabel":null,
           "children":[{"path":"/dev/sda1","type":"part","size":1,"fstype":"vfat","label":null,"partlabel":"EFI system partition"},
                       {"path":"/dev/sda3","type":"part","size":1,"fstype":"ntfs","label":null,"partlabel":"Basic data partition"}]},
          {"path":"/dev/sdb","type":"disk","size":16000000000,"model":"USB","tran":"usb","rm":true,"ro":false,"fstype":"iso9660","label":"MAZAPAN_202609","partlabel":null},
          {"path":"/dev/sr0","type":"rom","size":2000000000,"model":"QEMU DVD","tran":"sata","rm":true,"ro":false,"label":"MAZAPAN_202609"},
          {"path":"/dev/zram0","type":"disk","size":8000000000,"ro":false},
          {"path":"/dev/vdb","type":"disk","size":8000000000,"ro":"0","rm":"0"},
          {"path":"/dev/mmcblk0","type":"disk","size":64000000000,"ro":"1","rm":"0"},
          {"path":"/dev/sdc","type":"disk","size":64000000000,"ro":false,"rm":true,"label":null,
           "children":[{"path":"/dev/sdc1","type":"part","size":1,"fstype":"vfat","label":"MAZAPAN_2026","mountpoints":["/run/archiso/bootmnt"]}]},
          {"path":"/dev/sdd","type":"disk","size":64000000000,"ro":false,"rm":false,
           "children":[{"path":"/dev/sdd2","type":"part","size":1,"fstype":"swap","mountpoints":["[SWAP]"]}]},
          {"path":"/dev/nvme0n1","type":"disk","size":64000000000,"ro":false,"rm":false,
           "children":[{"path":"/dev/nvme0n1p2","type":"part","size":1,"fstype":"crypto_LUKS","mountpoints":[null],
             "children":[{"path":"/dev/mapper/root","type":"crypt","size":1,"fstype":"btrfs","mountpoints":["/mnt","/mnt/home"]}]}]}
        ]}
        """;

    [Fact]
    public void DisksLeaveOutWhatIsNotOneToInstallOn()
    {
        var d = Disks.Parse(Lsblk);
        // The stick this runs from (a label Rufus shortened), swap in use: out; a failed install's /mnt: in.
        Assert.Equal(["/dev/sda", "/dev/vdb", "/dev/nvme0n1"], d.Select(x => x.Path));
        Assert.Equal("Samsung SSD 870", d[0].Model);
        Assert.Equal(["windows"], d[0].Systems);
        Assert.Equal(2, d[0].Partitions);
        Assert.False(d[0].TooSmall);
        Assert.True(d[1].TooSmall);
    }

    const string ModelMap = """
        # Generated from system-config-keyboard's model list
        # consolelayout		xlayout	xmodel		xvariant	xoptions
        us			us	pc105+inet	-		terminate:ctrl_alt_bksp
        la-latin1		latam	pc105		-		terminate:ctrl_alt_bksp
        de-latin1-nodeadkeys	de	pc105		nodeadkeys	terminate:ctrl_alt_bksp
        de			de	pc105		-		terminate:ctrl_alt_bksp
        """;

    [Theory]
    [InlineData("latam", "", "la-latin1")]
    [InlineData("de", "nodeadkeys", "de-latin1-nodeadkeys")]
    [InlineData("de", "", "de")]
    [InlineData("de", "neo", "de")]
    [InlineData("xx", "", "us")]
    public void TheConsoleKeymapFollowsTheLayout(string layout, string variant, string keymap) =>
        Assert.Equal(keymap, Archinstall.ConsoleKeymap(ModelMap, layout, variant));

    [Fact]
    public void TheConfigurationIsTheWholeDisk()
    {
        var a = Answers.Parse(Good);
        using var doc = JsonDocument.Parse(Archinstall.Config(a, 512110190592, "la-latin1"));
        var r = doc.RootElement;
        var dev = r.GetProperty("disk_config").GetProperty("device_modifications")[0];
        Assert.Equal("/dev/vda", dev.GetProperty("device").GetString());
        Assert.True(dev.GetProperty("wipe").GetBoolean());
        var parts = dev.GetProperty("partitions");
        Assert.Equal("/efi", parts[0].GetProperty("mountpoint").GetString()); // /boot is on btrfs
        var start = parts[1].GetProperty("start").GetProperty("value").GetInt64();
        var size = parts[1].GetProperty("size").GetProperty("value").GetInt64();
        Assert.Equal(0, start % (1024 * 1024));
        Assert.True(start + size <= 512110190592 - 1024 * 1024);
        Assert.False(r.GetProperty("disk_config").TryGetProperty("disk_encryption", out _));
        Assert.Equal("la-latin1", r.GetProperty("locale_config").GetProperty("kb_layout").GetString());
        Assert.Equal("es_MX.UTF-8", r.GetProperty("locale_config").GetProperty("sys_lang").GetString());
        Assert.Contains("mazapan", r.GetProperty("packages").EnumerateArray().Select(x => x.GetString()));
        Assert.True(r.GetProperty("app_config").GetProperty("print_service_config").GetProperty("enabled").GetBoolean());
        Assert.Contains("avahi-daemon", r.GetProperty("services").EnumerateArray().Select(x => x.GetString()));
        // The password is only in the credentials.
        Assert.DoesNotContain("p4ss", r.GetRawText());
    }

    [Fact]
    public void EncryptedTheEfiPartitionIsBoot()
    {
        var a = Answers.Parse(With("encrypt", "true"));
        using var doc = JsonDocument.Parse(Archinstall.Config(a, 512110190592, "us"));
        var dc = doc.RootElement.GetProperty("disk_config");
        Assert.Equal("/boot", dc.GetProperty("device_modifications")[0].GetProperty("partitions")[0].GetProperty("mountpoint").GetString());
        var main = dc.GetProperty("device_modifications")[0].GetProperty("partitions")[1].GetProperty("obj_id").GetString();
        Assert.Equal(main, dc.GetProperty("disk_encryption").GetProperty("partitions")[0].GetString());
        using var creds = JsonDocument.Parse(Archinstall.Creds(a));
        Assert.Equal(a.Password, creds.RootElement.GetProperty("encryption_password").GetString());
        Assert.Equal(a.Password, creds.RootElement.GetProperty("users")[0].GetProperty("!password").GetString());
        // One password: greetd goes straight in that start, and the keyring
        // opens with the disk's password (not a keyring without one).
        Assert.DoesNotContain("--autologin", Decoded(Archinstall.Post(a)));
        // systemd's initramfs: the password stays in the kernel keyring for the login.
        Assert.Contains("s/\\bencrypt\\b/sd-encrypt/", Archinstall.Post(a));
        Assert.Contains("rd.luks.name=", Archinstall.Post(a));
        // systemd's initramfs either way (checkpoints start on an overlay through it).
        Assert.Contains("s/\\budev\\b/systemd/", Archinstall.Post(Answers.Parse(Good)));
        Assert.DoesNotContain("rd.luks.name=", Archinstall.Post(Answers.Parse(Good)));
        Assert.DoesNotContain("lock-on-idle=false", Decoded(Archinstall.Post(a)));
        Assert.Contains("autologin = true", Archinstall.UserConfig(a));
        Assert.Contains("pam-fde-boot-pw", doc.RootElement.GetProperty("packages").GetRawText());
        // Checkpoints in the boot menu too (their kernels kept on the EFI partition).
        Assert.Contains("enabled_plugins = [\"hw-snapshots\", \"hw-checkpoints\", \"theme-grub\", \"theme-plymouth\", \"update-ahead\", \"firewall\", \"login\"]", Archinstall.UserConfig(a));
        Assert.DoesNotContain("grub-btrfs", doc.RootElement.GetProperty("packages").GetRawText());
        Assert.Contains("greetd", doc.RootElement.GetProperty("packages").GetRawText());
    }

    [Theory]
    [InlineData("山田 太郎", "en", "", true)]
    [InlineData("김민준", "en", "", true)]
    [InlineData("Anna", "ja", "", true)]
    [InlineData("Anna", "en", "TW", true)]
    [InlineData("Анна Петрова", "ru", "RU", false)]
    [InlineData("محمد", "ar", "EG", false)]
    [InlineData("María", "es", "MX", false)]
    public void CjkFontsWhenThereIsCjkToShow(string name, string lang, string country, bool cjk)
    {
        var a = Answers.Parse(Good);
        a.FullName = name;
        a.Language = lang;
        a.Country = country;
        Assert.Equal(cjk, Archinstall.NeedsCjk(a));
    }

    [Theory]
    [InlineData("ja", "", true)]
    [InlineData("en", "KR", false)]
    [InlineData("zh_TW", "TW", true)]
    [InlineData("en", "US", false)]
    [InlineData("es", "MX", false)]
    public void AnInputMethodWhereCjkIsTyped(string lang, string country, bool on)
    {
        var a = Answers.Parse(Good);
        a.Language = lang;
        a.Country = country;
        Assert.Equal(on, Archinstall.TypesCjk(a));
        Assert.Equal(on, Archinstall.UserConfig(a).Contains("input-method"));
    }

    [Fact]
    public void TooSmallADiskIsRefused() =>
        Assert.Throws<MazapanException>(() => Archinstall.Config(Answers.Parse(Good), 4L * 1024 * 1024 * 1024, "us"));

    [Fact]
    public void HibernationGetsASwapFileOfItsOwn()
    {
        var a = Answers.Parse(With("hibernate", "true"));
        a.MemoryMiB = 8192;
        using var doc = JsonDocument.Parse(Archinstall.Config(a, 512110190592, "us"));
        Assert.Contains("\"@swap\"", doc.RootElement.GetProperty("disk_config").GetRawText());
        var post = Archinstall.Post(a);
        Assert.Contains("mkswapfile --size 8192m", post);
        Assert.Contains("resume_offset=", post);
        // Before GRUB's configuration is written, which takes resume= in.
        Assert.True(post.IndexOf("mkswapfile", StringComparison.Ordinal) < post.IndexOf("grub-mkconfig", StringComparison.Ordinal));
        Assert.Contains("\"hibernate\"", Archinstall.UserConfig(a));
        var none = Answers.Parse(With("hibernate", "false"));
        Assert.DoesNotContain("mkswapfile", Archinstall.Post(none));
        Assert.DoesNotContain("@swap", Archinstall.Config(none, 512110190592, "us"));
    }

    [Fact]
    public void MazapanUpdatesFromItsRepositoryOnceItsPublished()
    {
        var a = Answers.Parse(Good);
        Assert.DoesNotContain("[mazapan]", Archinstall.Post(a));
        var post = Archinstall.Post(a, "https://example.org/$channel/$arch");
        Assert.Contains("# mazapan channel: stable\n", Decoded(post));
        Assert.Contains("Server = https://example.org/stable/$arch\n", Decoded(post));
        Assert.Contains("[mazapan]", post);
        Assert.Contains("pacman-key --populate mazapan", post);
        // Before the first update, which needs it.
        Assert.True(post.IndexOf("[mazapan]", StringComparison.Ordinal) < post.IndexOf("pacman -Syu", StringComparison.Ordinal));
    }

    [Fact]
    public void AfterwardsTheAccountHasItsDesktop()
    {
        var a = Answers.Parse(Good);
        var post = Archinstall.Post(a);
        Assert.DoesNotContain("p4ss", post);
        Assert.Contains("grub-mkconfig -o /boot/grub/grub.cfg", post);
        var files = Decoded(post);
        Assert.Contains("theme = \"phosphor\"", files);
        Assert.Contains("language = \"es\"", files);
        Assert.Contains("kb_layout = \"latam\"", files);
        Assert.Contains("vscode\nsteam\n", files);
        Assert.Contains("start-hyprland", files);
        Assert.DoesNotContain("--autologin", files);
        Assert.DoesNotContain("Default_keyring", files); // PAM opens the login keyring
        Assert.Contains("enabled_plugins = [\"hw-snapshots\", \"hw-checkpoints\", \"theme-grub\", \"theme-plymouth\", \"update-ahead\", \"firewall\", \"login\"]", files);
        Assert.DoesNotContain("autologin", files); // not encrypted: the login screen, always
        // The first checkpoint is the first start's (hw-checkpoints): taken
        // here, the system's fstab wouldn't be in it yet.
        Assert.DoesNotContain("snapper --no-dbus -c root create", post);
        Assert.Contains("Option \"XkbLayout\" \"latam\"", files);
        Assert.Contains("mazapan apply --system -y", post);
        Assert.DoesNotContain("hardware --enable", post); // worked out before, from the ISO
        Assert.Contains("--removable", post);
        // Arch's pacman.conf, not the ISO's offline-only one; multilib on; synced when online.
        Assert.Contains("bsdtar -xOf \"$p\" etc/pacman.conf", post);
        Assert.Contains("[core]", files);
        Assert.Contains("s/^#//", post);
        Assert.Contains("pacman -Syu --noconfirm", post);
        Assert.True(post.IndexOf("pacman -Syu") < post.IndexOf("mazapan apply"));
        Assert.Contains("usermod -c \"$(printf %s " + Convert.ToBase64String(Encoding.UTF8.GetBytes("Rick")) + " | base64 -d)\" rick", post);
        Assert.True(post.IndexOf("mazapan apply") < post.IndexOf("chown -R"));
    }

    /// <summary>Post's files, decoded (they go in as base64).</summary>
    internal static string Decoded(string post) => string.Join("\n",
        System.Text.RegularExpressions.Regex.Matches(post, @"printf %s ([A-Za-z0-9+/=]+) \| base64 -d")
            .Select(m => Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[1].Value))));
}

public class LocalesTests
{
    const string ZoneTab = "# comment\nPR,AG,CA,AI,AW\t+182806-0660622\tAmerica/Puerto_Rico\nMX\t+1924-09909\tAmerica/Mexico_City\tCentral Mexico\nES\t+4024-00341\tEurope/Madrid\tSpain\nCH,DE,LI\t+4723+00832\tEurope/Zurich\nUS\t+404251-0740023\tAmerica/New_York\n";
    const string Supported = "ja_JP.UTF-8 UTF-8\nzh_CN.UTF-8 UTF-8\nfr_FR.UTF-8 UTF-8\nfr_CA.UTF-8 UTF-8\npt_BR.UTF-8 UTF-8\nen_US.UTF-8 UTF-8\nen_AG.UTF-8 UTF-8\nen_PR.UTF-8 UTF-8\nes_ES.UTF-8 UTF-8\nes_MX.UTF-8 UTF-8\nde_DE.UTF-8 UTF-8\nde_CH.UTF-8 UTF-8\nes_MX ISO-8859-1\n";

    [Theory]
    [InlineData("es", "America/Mexico_City", "es_MX.UTF-8")]
    [InlineData("en", "America/Mexico_City", "en_US.UTF-8")]
    [InlineData("es", "America/New_York", "es_ES.UTF-8")]
    [InlineData("de", "Europe/Zurich", "de_CH.UTF-8")]
    [InlineData("es", "UTC", "es_ES.UTF-8")]
    [InlineData("es_MX", "Europe/Madrid", "es_MX.UTF-8")]
    [InlineData("en", "America/Puerto_Rico", "en_PR.UTF-8")]
    [InlineData("xx", "UTC", "en_US.UTF-8")]
    [InlineData("ja", "UTC", "ja_JP.UTF-8")]
    [InlineData("zh", "Europe/Madrid", "zh_CN.UTF-8")]
    [InlineData("fr", "UTC", "fr_FR.UTF-8")]
    [InlineData("pt", "UTC", "pt_BR.UTF-8")]
    public void TheLocaleIsTheLanguageWhereYouAre(string lang, string zone, string locale) =>
        Assert.Equal(locale, Locales.For(lang, zone, ZoneTab, Supported));

    [Fact]
    public void BadJsonIsSaidAsSuch()
    {
        Assert.Throws<Mazapan.Util.MazapanException>(() => Answers.Parse("{"));
        Assert.Throws<Mazapan.Util.MazapanException>(() => Answers.Parse("[]"));
    }

    [Fact]
    public void EncryptedThePasswordIsWhatAnyKeyboardTypes()
    {
        const string a = """{"disk":"/dev/vda","user":"rick","fullname":"R","theme":"paper","encrypt":true,"password":""";
        Assert.Throws<Mazapan.Util.MazapanException>(() => Answers.Parse(a + "\"añejo añejo\"}"));
        Assert.Equal("a-Z_9!a-Z_9!", Answers.Parse(a + "\"a-Z_9!a-Z_9!\"}").Password);
        Assert.Equal("añejo añejo", Answers.Parse(a.Replace("true", "false") + "\"añejo añejo\"}").Password);
        // It opens the disk, the account and the keyring: never a short one.
        Assert.Throws<Mazapan.Util.MazapanException>(() => Answers.Parse(a + "\"short\"}"));
    }
}

public class InstallProgressTests
{
    [Theory]
    [InlineData("Wiping partitions and metadata: /dev/vda", "disk")]
    [InlineData("Installing packages: ['base', 'sudo', 'linux-firmware']", "base")]
    [InlineData("Adding bootloader Grub to /dev/vda1", "boot")]
    [InlineData("Creating user ricardo", "account")]
    [InlineData("Creating user 'polkitd' (User for polkitd) with UID 102 and GID 102.", null)]
    [InlineData("Creating user ricardos", null)]
    [InlineData("Installing packages: ['mazapan', 'base-devel']", "desktop")]
    [InlineData("Executing custom command \"set -eu", "finish")]
    [InlineData("( 48/158) installing libgpg-error", null)]
    public void ArchinstallsLinesSayTheStep(string line, string? step) =>
        Assert.Equal(step, Mazapan.Cli.Program.InstallStep(line, "ricardo"));

    [Fact]
    public void OnlyPlainKeysGoToTheNewAccount()
    {
        var keys = Answers.Keys("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHk mazapan-vm\n" +
            "command=\"rm -rf /\" ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHk x\n# comment\n\nssh-rsa AAAAB3NzaC1yc2E= a@b\nssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHk mazapan-vm\n");
        Assert.Equal(["ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHk mazapan-vm", "ssh-rsa AAAAB3NzaC1yc2E= a@b"], keys);
    }

    [Fact]
    public void WithKeysTheAccountHasSsh()
    {
        var a = Answers.Parse("""{"disk":"/dev/vda","user":"rick","fullname":"R","password":"xxxxxxxx","theme":"paper"}""");
        a.SshKeys = ["ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIHk mazapan-vm"];
        using var doc = System.Text.Json.JsonDocument.Parse(Archinstall.Config(a, 64L << 30, "us"));
        Assert.Contains("openssh", doc.RootElement.GetProperty("packages").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("sshd", doc.RootElement.GetProperty("services").EnumerateArray().Select(x => x.GetString()));
        var post = Archinstall.Post(a);
        Assert.Contains("/home/rick/.ssh/authorized_keys", post);
        Assert.Contains("PasswordAuthentication no", SetupTests.Decoded(post));
        Assert.True(post.IndexOf("authorized_keys") < post.IndexOf("chown -R"));
    }

    [Theory]
    [InlineData("es_MX", "tesseract-data-spa")]
    [InlineData("ja", "tesseract-data-jpn")]
    [InlineData("en", null)]
    [InlineData("xx", null)]
    public void TextInPicturesIsReadInTheLanguage(string lang, string? data) =>
        Assert.Equal(data, Mazapan.Setup.Archinstall.OcrData(lang));

    [Fact]
    public void HibernationOnlyWhereItFitsAndNeverStopsTheInstall()
    {
        const long GiB = 1L << 30;
        Assert.True(Archinstall.HibernationFits(512 * GiB, 32 * 1024, true));
        Assert.False(Archinstall.HibernationFits(32 * GiB, 32 * 1024, true));
        var a = Answers.Parse(SetupTests.WithKey("encrypt", "true"));
        a.Hibernate = true;
        a.MemoryMiB = 16 * 1024;
        var post = Archinstall.Post(a);
        // The swap file failing: no resume, the plugin taken off, the rest goes on.
        Assert.Contains("hibernate=1; if btrfs filesystem mkswapfile", post);
        Assert.Contains("else rm -f /swap/swapfile; hibernate=0;", post);
        Assert.Contains("[ \"$hibernate\" = 1 ] || sed -i", post);
        var f = Path.GetTempFileName();
        File.WriteAllText(f, post);
        var p = System.Diagnostics.Process.Start("bash", ["-n", f])!;
        p.WaitForExit();
        File.Delete(f);
        Assert.Equal(0, p.ExitCode);
    }
}

