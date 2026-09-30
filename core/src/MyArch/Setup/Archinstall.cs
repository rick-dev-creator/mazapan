using System.Text;
using MyArch.Util;

namespace MyArch.Setup;

/// <summary>
/// Archinstall turns the answers into archinstall's configuration: the
/// whole disk, GPT, an EFI partition and btrfs on the rest (subvolumes for
/// /, /home, the logs and pacman's cache), GRUB, NetworkManager, PipeWire,
/// swap in zram, and myarch. Without encryption /boot is on btrfs, inside
/// the root subvolume, so a snapshot boots with its own kernel; encrypted,
/// /boot is the EFI partition (GRUB can't open the LUKS2 archinstall makes).
/// Then, in the new system (its custom_commands): the account's myarch
/// configuration, and its desktop on tty1.
/// </summary>
public static class Archinstall
{
    const long MiB = 1024 * 1024;

    /// <summary>What goes in besides archinstall's own: the desktop and what building from the AUR needs.</summary>
    public static readonly string[] Packages = ["myarch", "base-devel", "git", "sof-firmware", "reflector"];

    /// <summary>Mirrors the new system uses once installed (the install itself uses the ISO's).</summary>
    public static readonly string[] Mirrors =
    [
        "https://geo.mirror.pkgbuild.com/$repo/os/$arch",
        "https://mirror.rackspace.com/archlinux/$repo/os/$arch",
    ];

    public static string Config(Answers a, long diskBytes, string consoleKeymap)
    {
        var esp = a.Encrypt ? 2048 * MiB : 512 * MiB;
        var mainStart = MiB + esp;
        // Whole MiBs, and one left at the end for GPT's backup table.
        var mainSize = diskBytes / MiB * MiB - mainStart - MiB;
        if (mainSize < 8 * 1024 * MiB) throw new MyArchException("the disk is too small");
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
            } },
            { "bootloader_config", new Fields { { "bootloader", "Grub" }, { "uki", false }, { "removable", false } } },
            { "custom_commands", new List<object> { Post(a) } },
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
            { "packages", Packages.Concat(a.Encrypt ? [] : ["greetd"]).Concat(a.SshKeys.Count > 0 ? ["openssh"] : [])
                .Concat(NeedsCjk(a) ? ["noto-fonts-cjk"] : []).Cast<object>().ToList() },
            { "parallel_downloads", 8 },
            { "services", new List<object> { "power-profiles-daemon" }.Concat(a.SshKeys.Count > 0 ? ["sshd"] : []).ToList() },
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
    /// account's desktop is written here (myarch apply, as it, with the
    /// system files), so the first start goes straight to it: the login
    /// screen (plugin login), or, encrypted, straight in (the disk's
    /// password was the login).
    /// </summary>
    public static string Post(Answers a)
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
            s.Append("install -d /etc/systemd/system/getty@tty1.service.d\n");
            s.Append(Write("/etc/systemd/system/getty@tty1.service.d/autologin.conf",
                $"[Service]\nExecStart=\nExecStart=-/usr/bin/agetty --noreset --noclear --autologin {a.User} - ${{TERM}}\n"));
            s.Append("grub-install --target=x86_64-efi --efi-directory=/boot --boot-directory=/boot --removable\n");
        }
        // Arch's own pacman.conf (archinstall copies the ISO's, which only
        // knows the offline repository), from the pacman package the install
        // left in the cache; multilib on (Steam and other 32-bit software).
        s.Append("if p=$(ls /var/cache/pacman/pkg/pacman-[0-9]*.pkg.tar.zst 2>/dev/null | head -n 1) && [ -n \"$p\" ] && bsdtar -xOf \"$p\" etc/pacman.conf > /etc/pacman.conf.myarch && grep -q '^\\[core\\]' /etc/pacman.conf.myarch; " +
            "then mv /etc/pacman.conf.myarch /etc/pacman.conf; else rm -f /etc/pacman.conf.myarch; " +
            Write("/etc/pacman.conf", PacmanConf).TrimEnd('\n') + "; fi\n");
        s.Append("sed -i -e '/^#\\[multilib\\]/,/^#Include/ s/^#//' -e 's/^#Color/Color/' /etc/pacman.conf\n");
        s.Append("rm -f /var/lib/pacman/sync/offline.*\n");
        // The mirrors nearest to where you are, when there's a connection
        // now (else the worldwide ones stay).
        if (a.Country != "")
            s.Append($"if reflector --country {a.Country} --protocol https --latest 12 --sort score --connection-timeout 3 --save /etc/pacman.d/mirrorlist.myarch >/dev/null 2>&1 " +
                "&& grep -q '^Server' /etc/pacman.d/mirrorlist.myarch; then mv /etc/pacman.d/mirrorlist.myarch /etc/pacman.d/mirrorlist; else rm -f /etc/pacman.d/mirrorlist.myarch; fi\n");
        // The system's keyboard, where localectl keeps it (the login screen reads it).
        s.Append("install -d /etc/X11/xorg.conf.d\n");
        s.Append(Write("/etc/X11/xorg.conf.d/00-keyboard.conf",
            $"Section \"InputClass\"\n    Identifier \"system-keyboard\"\n    MatchIsKeyboard \"on\"\n" +
            $"    Option \"XkbLayout\" \"{a.KbLayout}\"\n    Option \"XkbVariant\" \"{a.KbVariant}\"\nEndSection\n"));
        // The full name (the login screen shows it): archinstall doesn't set it.
        if (a.FullName != "")
            s.Append($"usermod -c \"$(printf %s {Convert.ToBase64String(Files.Utf8.GetBytes(a.FullName))} | base64 -d)\" {a.User}\n");
        var home = "/home/" + a.User;
        s.Append($"install -d -o {a.User} -g {a.User} {home}/.config {home}/.config/myarch {home}/.local {home}/.local/state {home}/.local/state/myarch {home}/.cache\n");
        s.Append(Write($"{home}/.config/myarch/config.toml", UserConfig(a)));
        // The first login opens the welcome where the installer left off.
        s.Append(Write($"{home}/.local/state/myarch/welcome.json", "{\"step\":\"look\",\"done\":false}\n"));
        if (a.Apps.Count > 0) s.Append(Write($"{home}/.local/state/myarch/first-apps", string.Join('\n', a.Apps) + "\n"));
        s.Append(Write($"{home}/.bash_profile", BashProfile, append: true));
        if (a.SshKeys.Count > 0)
        {
            // Keys only: the account's password never goes over the network.
            s.Append("install -d /etc/ssh/sshd_config.d\n");
            s.Append(Write("/etc/ssh/sshd_config.d/10-myarch.conf", "# myarch install: the keys the live system was reached with, no passwords.\nPasswordAuthentication no\nKbdInteractiveAuthentication no\nPermitRootLogin no\n"));
            s.Append($"install -d -m 700 {home}/.ssh\n");
            s.Append(Write($"{home}/.ssh/authorized_keys", string.Join('\n', a.SshKeys) + "\n"));
            s.Append($"chmod 600 {home}/.ssh/authorized_keys\n");
        }
        // Online: the repositories, and what's newer than the ISO, now (the
        // first start is then up to date). Offline, the first app install does it.
        s.Append("if curl -fsS --max-time 5 -o /dev/null https://geo.mirror.pkgbuild.com/; then pacman -Syu --noconfirm > /var/log/myarch-first-update.log 2>&1 || echo \"pacman -Syu failed: /var/log/myarch-first-update.log\"; fi\n");
        // Its desktop, now (reloads fail here, nothing runs yet: warnings only).
        s.Append($"HOME={home} USER={a.User} myarch apply --system -y > /var/log/myarch-first-apply.log 2>&1 || echo \"myarch apply failed: /var/log/myarch-first-apply.log\"\n");
        s.Append($"chown -R {a.User}:{a.User} {home}\n");
        return s.ToString();
    }

    static string Write(string path, string text, bool append = false) =>
        $"printf %s {Convert.ToBase64String(Files.Utf8.GetBytes(text))} | base64 -d {(append ? ">>" : ">")} {path}\n";

    public static string UserConfig(Answers a)
    {
        var c = new Config.Settings { Theme = a.Theme, Language = a.Language };
        // The login screen, unless the disk's password is the login.
        if (!a.Encrypt) c.Enabled.Add("login");
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

        # myarch: the desktop on tty1. The first time, myarch writes it.
        # Not exec'd: if Hyprland stops there's a shell here, not a loop.
        if [[ -z $WAYLAND_DISPLAY && $(tty) == /dev/tty1 ]]; then
          [[ -e ~/.config/hypr/hyprland.lua ]] || myarch apply >~/.cache/myarch-first-apply.log 2>&1
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
