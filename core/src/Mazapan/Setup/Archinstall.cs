using System.Text;
using Mazapan.Util;

namespace Mazapan.Setup;

/// <summary>
/// Archinstall turns the answers into archinstall's configuration: the
/// whole disk, GPT, an EFI partition and btrfs on the rest (subvolumes for
/// /, /home, the logs and pacman's cache), GRUB, NetworkManager, PipeWire,
/// swap in zram, and mazapan. Without encryption /boot is on btrfs, inside
/// the root subvolume, so a snapshot boots with its own kernel; encrypted,
/// /boot is the EFI partition (GRUB can't open the LUKS2 archinstall makes).
/// Then, in the new system (its custom_commands): the account's mazapan
/// configuration, and its desktop on tty1.
/// </summary>
public static class Archinstall
{
    const long MiB = 1024 * 1024;

    /// <summary>What goes in besides archinstall's own: the desktop and what building from the AUR needs.</summary>
    public static readonly string[] Packages = ["mazapan", "mazapan-keyring", "ufw", "base-devel", "git", "sof-firmware", "reflector", "avahi"];

    /// <summary>Mirrors the new system uses once installed (the install itself uses the ISO's).</summary>
    public static readonly string[] Mirrors =
    [
        "https://geo.mirror.pkgbuild.com/$repo/os/$arch",
        "https://mirror.rackspace.com/archlinux/$repo/os/$arch",
    ];

    /// <summary>
    /// Snapshots from day one (plugins hw-snapshots and, with /boot on
    /// btrfs, hw-snapshots-grub: bootable from the menu): what they need,
    /// so turning them on while installing downloads nothing.
    /// </summary>
    static string[] Snapshots(Answers a) =>
        a.Encrypt ? ["snapper", "snap-pac"] : ["snapper", "snap-pac", "grub-btrfs", "inotify-tools"];

    public static string Config(Answers a, long diskBytes, string consoleKeymap, string server = "")
    {
        var esp = a.Encrypt ? 2048 * MiB : 512 * MiB;
        var mainStart = MiB + esp;
        // Whole MiBs, and one left at the end for GPT's backup table.
        var mainSize = diskBytes / MiB * MiB - mainStart - MiB;
        if (mainSize < 8 * 1024 * MiB) throw new MazapanException("the disk is too small");
        Fields Size(long bytes) => new()
        {
            { "sector_size", new Fields { { "unit", "B" }, { "value", 512 } } },
            { "unit", "B" }, { "value", bytes },
        };
        Fields Subvol(string name, string mount) => new() { { "mountpoint", mount }, { "name", name } };
        const string espId = "6b1f0c52-5a3e-4d8e-9a7c-3c2f9e1d0a01", mainId = "6b1f0c52-5a3e-4d8e-9a7c-3c2f9e1d0a02";

        var disk = new Fields
        {
            { "config_type", "default_layout" },
            { "device_modifications", new List<object> { new Fields {
                { "device", a.Disk },
                { "partitions", new List<object> {
                    new Fields {
                        { "btrfs", new List<object>() }, { "dev_path", null }, { "flags", new List<object> { "boot", "esp" } },
                        { "fs_type", "fat32" }, { "mount_options", new List<object>() },
                        // /efi holds GRUB alone: /boot is moved onto btrfs afterwards (Post).
                        { "mountpoint", a.Encrypt ? "/boot" : "/efi" },
                        { "obj_id", espId }, { "size", Size(esp) }, { "start", Size(MiB) }, { "status", "create" }, { "type", "primary" },
                    },
                    new Fields {
                        { "btrfs", new List<object> { Subvol("@", "/"), Subvol("@home", "/home"), Subvol("@log", "/var/log"), Subvol("@pkg", "/var/cache/pacman/pkg") } },
                        { "dev_path", null }, { "flags", new List<object>() }, { "fs_type", "btrfs" },
                        { "mount_options", new List<object> { "compress=zstd" } }, { "mountpoint", null },
                        { "obj_id", mainId }, { "size", Size(mainSize) }, { "start", Size(mainStart) }, { "status", "create" }, { "type", "primary" },
                    },
                } },
                { "wipe", true },
            } } },
        };
        if (a.Encrypt)
            disk.Add("disk_encryption", new Fields
            {
                { "encryption_type", "luks" }, { "lvm_volumes", new List<object>() }, { "iter_time", 2000 },
                { "partitions", new List<object> { mainId } },
            });

        var c = new Fields
        {
            { "archinstall-language", "English" },
            { "app_config", new Fields {
                { "audio_config", new Fields { { "audio", "pipewire" } } },
                { "bluetooth_config", new Fields { { "enabled", true } } },
                // Printing: CUPS, its settings app, managing printers without a terminal.
                { "print_service_config", new Fields { { "enabled", true } } },
            } },
            { "bootloader_config", new Fields { { "bootloader", "Grub" }, { "uki", false }, { "removable", false } } },
            { "custom_commands", new List<object> { Post(a, server) } },
            { "disk_config", disk },
            { "hostname", a.Hostname },
            { "kernels", new List<object> { "linux" } },
            { "locale_config", new Fields { { "kb_layout", consoleKeymap }, { "sys_enc", "UTF-8" }, { "sys_lang", a.Locale } } },
            { "mirror_config", new Fields {
                { "custom_repositories", new List<object>() },
                { "custom_servers", Mirrors.Select(m => (object)new Fields { { "url", m } }).ToList() },
                { "mirror_regions", new Fields() }, { "optional_repositories", new List<object>() },
            } },
            { "network_config", new Fields { { "type", "nm" } } },
            { "ntp", true },
            { "packages", Packages.Concat(Snapshots(a)).Concat(a.HardwarePackages).Concat(["greetd", "pam-fde-boot-pw"]).Concat(a.SshKeys.Count > 0 ? ["openssh"] : [])
                .Concat(NeedsCjk(a) ? ["noto-fonts-cjk"] : [])
                .Concat(TypesCjk(a) ? ["fcitx5", "fcitx5-gtk", "fcitx5-qt", "fcitx5-configtool", "fcitx5-mozc", "fcitx5-chinese-addons", "fcitx5-hangul"] : [])
                .Cast<object>().ToList() },
            { "parallel_downloads", 8 },
            // avahi: printers (and other devices) on the network found by themselves.
            { "services", new List<object> { "power-profiles-daemon", "avahi-daemon" }.Concat(a.SshKeys.Count > 0 ? ["sshd"] : []).ToList() },
            { "swap", true },
            { "timezone", a.Timezone },
        };
        return GoJson.Marshal(c) + "\n";
    }

    /// <summary>
    /// NeedsCjk: Chinese, Japanese or Korean text to show (the account's
    /// name, the language, where you are): their fonts, which the rest of the
    /// world's scripts don't need (noto-fonts has those).
    /// </summary>
    public static bool NeedsCjk(Answers a) =>
        a.FullName.Any(c => c is >= '\u2E80' and <= '\u9FFF' or >= '\uAC00' and <= '\uD7AF' or >= '\uF900' and <= '\uFAFF' or >= '\uFF00' and <= '\uFFEF')
        || a.Language.Split('_')[0] is "zh" or "ja" or "ko"
        || a.Country is "CN" or "JP" or "KR" or "TW" or "HK" or "MO";

    /// <summary>
    /// Chinese, Japanese or Korean to type: the language (fcitx5 sets its
    /// method up from it; someone in Tokyo writing English wouldn't get one).
    /// </summary>
    public static bool TypesCjk(Answers a) => a.Language.Split('_')[0] is "zh" or "ja" or "ko";

    /// <summary>The secrets, in their own file (archinstall's --creds).</summary>
    public static string Creds(Answers a)
    {
        var c = new Fields
        {
            { "users", new List<object> { new Fields {
                { "!password", a.Password }, { "groups", new List<object> { "wheel" } }, { "sudo", true }, { "username", a.User },
            } } },
        };
        if (a.Encrypt) c.Add("encryption_password", a.Password);
        return GoJson.Marshal(c) + "\n";
    }

    /// <summary>
    /// Post: run in the new system as root, last. Every value from the
    /// answers goes in base64 or already checked (Answers.Check). The
    /// account's desktop is written here (mazapan apply, as it, with the
    /// system files), so the first start goes straight to it: the login
    /// screen (plugin login), or, encrypted, straight in (the disk's
    /// password was the login).
    /// </summary>
    public static string Post(Answers a, string server = "")
    {
        var s = new StringBuilder("set -eu\n");
        if (!a.Encrypt)
        {
            // GRUB's own files on btrfs, next to the kernels: the EFI
            // partition only keeps the loader that finds them.
            s.Append("grub-install --target=x86_64-efi --efi-directory=/efi --bootloader-id=GRUB\n");
            s.Append("grub-mkconfig -o /boot/grub/grub.cfg\n");
            s.Append("rm -rf /efi/grub\n");
            // Also where firmware looks without being told (some ignore their boot entries).
            s.Append("grub-install --target=x86_64-efi --efi-directory=/efi --removable\n");
        }
        else
        {
            // systemd's initramfs (sd-encrypt, sd-vconsole), as the Arch Wiki
            // and every systemd distro: archinstall makes the older one
            // (encrypt, keymap) unless there's a security key. With systemd's,
            // the password typed as it starts stays in the kernel keyring,
            // where the login takes it (pam_fde_boot_pw: one password), and
            // the installer's keyboard is in it (sd-vconsole: the password is
            // typed in the layout it was chosen in).
            s.Append("sed -i -E '/^HOOKS=/{s/\\budev\\b/systemd/; s/\\bkeymap consolefont\\b/sd-vconsole/; s/\\bencrypt\\b/sd-encrypt/}' /etc/mkinitcpio.conf\n");
            s.Append("sed -i -E 's/cryptdevice=UUID=([^: ]+):root/rd.luks.name=\\1=root/' /etc/default/grub\n");
            s.Append("grep -q '^HOOKS=.*sd-encrypt' /etc/mkinitcpio.conf && grep -q 'rd.luks.name=' /etc/default/grub\n");
            s.Append("mkinitcpio -P\n");
            s.Append("grub-mkconfig -o /boot/grub/grub.cfg\n");
            s.Append("grub-install --target=x86_64-efi --efi-directory=/boot --boot-directory=/boot --removable\n");
        }
        // Arch's own pacman.conf (archinstall copies the ISO's, which only
        // knows the offline repository), from the pacman package the install
        // left in the cache; multilib on (Steam and other 32-bit software).
        s.Append("if p=$(ls /var/cache/pacman/pkg/pacman-[0-9]*.pkg.tar.zst 2>/dev/null | head -n 1) && [ -n \"$p\" ] && bsdtar -xOf \"$p\" etc/pacman.conf > /etc/pacman.conf.mazapan && grep -q '^\\[core\\]' /etc/pacman.conf.mazapan; " +
            "then mv /etc/pacman.conf.mazapan /etc/pacman.conf; else rm -f /etc/pacman.conf.mazapan; " +
            Write("/etc/pacman.conf", PacmanConf).TrimEnd('\n') + "; fi\n");
        s.Append("sed -i -e '/^#\\[multilib\\]/,/^#Include/ s/^#//' -e 's/^#Color/Color/' /etc/pacman.conf\n");
        s.Append("rm -f /var/lib/pacman/sync/offline.*\n");
        // Mazapán's own repository, so it updates itself: the stable channel,
        // its keys in pacman's keyring. Not while it isn't published (server
        // empty): pacman fails on a repository it can't reach.
        // Only with its keys in pacman's keyring: without them its packages
        // would stop every update. A failure here never stops the install.
        if (server != "")
            s.Append($"if [ -f {Pacman.Repository.Keyring} ] && pacman-key -l >/dev/null 2>&1 && pacman-key --populate mazapan >/dev/null 2>&1; then " +
                Write(Pacman.Repository.Mirrorlist, Pacman.Repository.MirrorlistText(server, "stable")).TrimEnd('\n') + "; " +
                Pacman.Repository.EnableScript + "; else echo \"Mazapán's repository not added: its keys aren't there\"; fi\n");
        // pacman's cache pruned weekly, three versions of each package kept:
        // what a rollback takes the previous one from (pacman-contrib).
        s.Append("systemctl enable paccache.timer\n");
        // The mirrors nearest to where you are, when there's a connection
        // now (else the worldwide ones stay).
        if (a.Country != "")
            s.Append($"if reflector --country {a.Country} --protocol https --latest 12 --sort score --connection-timeout 3 --save /etc/pacman.d/mirrorlist.mazapan >/dev/null 2>&1 " +
                "&& grep -q '^Server' /etc/pacman.d/mirrorlist.mazapan; then mv /etc/pacman.d/mirrorlist.mazapan /etc/pacman.d/mirrorlist; else rm -f /etc/pacman.d/mirrorlist.mazapan; fi\n");
        // The system's keyboard, where localectl keeps it (the login screen reads it).
        s.Append("install -d /etc/X11/xorg.conf.d\n");
        s.Append(Write("/etc/X11/xorg.conf.d/00-keyboard.conf",
            $"Section \"InputClass\"\n    Identifier \"system-keyboard\"\n    MatchIsKeyboard \"on\"\n" +
            $"    Option \"XkbLayout\" \"{a.KbLayout}\"\n    Option \"XkbVariant\" \"{a.KbVariant}\"\nEndSection\n"));
        // The full name (the login screen shows it): archinstall doesn't set it.
        if (a.FullName != "")
            s.Append($"usermod -c \"$(printf %s {Convert.ToBase64String(Files.Utf8.GetBytes(a.FullName))} | base64 -d)\" {a.User}\n");
        var home = "/home/" + a.User;
        s.Append($"install -d -o {a.User} -g {a.User} {home}/.config {home}/.config/mazapan {home}/.local {home}/.local/state {home}/.local/state/mazapan {home}/.cache\n");
        s.Append(Write($"{home}/.config/mazapan/config.toml", UserConfig(a)));
        // The first login opens the welcome where the installer left off.
        s.Append(Write($"{home}/.local/state/mazapan/welcome.json", "{\"step\":\"look\",\"done\":false}\n"));
        if (a.Apps.Count > 0) s.Append(Write($"{home}/.local/state/mazapan/first-apps", string.Join('\n', a.Apps) + "\n"));
        s.Append(Write($"{home}/.bash_profile", BashProfile, append: true));
        if (a.SshKeys.Count > 0)
        {
            // Keys only: the account's password never goes over the network.
            s.Append("install -d /etc/ssh/sshd_config.d\n");
            s.Append(Write("/etc/ssh/sshd_config.d/10-mazapan.conf", "# mazapan install: the keys the live system was reached with, no passwords.\nPasswordAuthentication no\nKbdInteractiveAuthentication no\nPermitRootLogin no\n"));
            s.Append($"install -d -m 700 {home}/.ssh\n");
            s.Append(Write($"{home}/.ssh/authorized_keys", string.Join('\n', a.SshKeys) + "\n"));
            s.Append($"chmod 600 {home}/.ssh/authorized_keys\n");
        }
        // Online: the repositories, and what's newer than the ISO, now (the
        // first start is then up to date). Offline, the first app install does it.
        s.Append("if curl -fsS --max-time 5 -o /dev/null https://geo.mirror.pkgbuild.com/; then pacman -Syu --noconfirm > /var/log/mazapan-first-update.log 2>&1 || echo \"pacman -Syu failed: /var/log/mazapan-first-update.log\"; fi\n");
        // Its desktop, now (reloads fail here, nothing runs yet: warnings only).
        s.Append($"HOME={home} USER={a.User} mazapan apply --system -y > /var/log/mazapan-first-apply.log 2>&1 || echo \"mazapan apply failed: /var/log/mazapan-first-apply.log\"\n");
        s.Append($"chown -R {a.User}:{a.User} {home}\n");
        // The first one: the system as installed, to go back to.
        s.Append("if grep -qE '^SNAPPER_CONFIGS=.*[\" ]root[\" ]' /etc/conf.d/snapper 2>/dev/null; then snapper --no-dbus -c root create -c number -d 'mazapan installed' --userdata important=yes || true; fi\n");
        // In the boot menu already (its daemon only sees the ones that come later).
        s.Append("if [ -x /etc/grub.d/41_snapshots-btrfs ] && [ -e /etc/systemd/system/grub-btrfsd.service.d/mazapan-snapshots.conf ]; then GRUB_BTRFS_SNAPSHOT_KERNEL_PARAMETERS=systemd.volatile=overlay /etc/grub.d/41_snapshots-btrfs >/dev/null 2>&1 || true; fi\n");
        return s.ToString();
    }

    static string Write(string path, string text, bool append = false) =>
        $"printf %s {Convert.ToBase64String(Files.Utf8.GetBytes(text))} | base64 -d {(append ? ">>" : ">")} {path}\n";

    public static string UserConfig(Answers a)
    {
        var c = new Config.Settings { Theme = a.Theme, Language = a.Language };
        // Snapshots before and after every package change, from the start;
        // in the boot menu when /boot is on btrfs (each has its kernel).
        c.Enabled.Add("hw-snapshots");
        if (!a.Encrypt) c.Enabled.Add("hw-snapshots-grub");
        // The firewall: nothing in that wasn't asked for; SSH too when the
        // install was given keys to be reached with.
        c.Enabled.Add("firewall");
        if (a.SshKeys.Count > 0) c.Plugins["firewall"] = new(StringComparer.Ordinal) { ["ssh"] = true };
        // The login screen; encrypted, straight in once each start (the disk's
        // password, just typed, was the login, and opens the keyring too).
        c.Enabled.Add("login");
        if (a.Encrypt) c.Plugins["login"] = new(StringComparer.Ordinal) { ["autologin"] = true };
        // Typing Chinese, Japanese, Korean, where it's the language or the place.
        if (TypesCjk(a)) c.Enabled.Add("input-method");
        // This machine's hardware plugins (its GPU's drivers…), their packages
        // installed with the system.
        foreach (var h in a.Hardware) if (!c.Enabled.Contains(h)) c.Enabled.Add(h);
        c.Plugins["hypr-base"] = new(StringComparer.Ordinal) { ["kb_layout"] = a.KbLayout, ["kb_variant"] = a.KbVariant };
        return c.Text();
    }

    /// <summary>Arch's pacman.conf, for when the pacman package isn't in the cache to take it from.</summary>
    const string PacmanConf = """
        [options]
        HoldPkg     = pacman glibc
        Architecture = auto
        ParallelDownloads = 5
        SigLevel    = Required DatabaseOptional
        LocalFileSigLevel = Optional

        [core]
        Include = /etc/pacman.d/mirrorlist

        [extra]
        Include = /etc/pacman.d/mirrorlist

        #[multilib]
        #Include = /etc/pacman.d/mirrorlist

        """;

    const string BashProfile = """

        # mazapan: the desktop on tty1. The first time, and whenever Mazapán
        # changed since (an update by hand), mazapan writes it.
        # Not exec'd: if Hyprland stops there's a shell here, not a loop.
        if [[ -z $WAYLAND_DISPLAY && $(tty) == /dev/tty1 ]]; then
          mazapan apply --if-updated >~/.cache/mazapan-login-apply.log 2>&1
          start-hyprland >~/.cache/start-hyprland.log 2>&1
        fi

        """;

    /// <summary>
    /// ConsoleKeymap: the console's keymap for an X layout (the LUKS prompt
    /// and the TTYs use it), from systemd's kbd-model-map; "us" when none.
    /// </summary>
    public static string ConsoleKeymap(string modelMap, string layout, string variant)
    {
        string? any = null;
        foreach (var line in modelMap.Split('\n'))
        {
            var f = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 4 || f[0].StartsWith('#') || f[1] != layout) continue;
            if (f[3] == (variant == "" ? "-" : variant)) return f[0];
            // A variant the console doesn't have: the layout's plain one.
            if (f[3] == "-" || any == null) any = f[0];
        }
        return any ?? "us";
    }
}
