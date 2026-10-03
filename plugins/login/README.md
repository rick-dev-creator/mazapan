# Login screen

The login screen in the theme: the clock, your name and your password,
nothing else. greetd starts it, in a Hyprland of its own with a Quickshell
greeter drawn in the theme's colors, as greetd's greeter user (it can't
read anyone's files). The keyboard is the system's
(`/etc/X11/xorg.conf.d/00-keyboard.conf`, what `localectl set-x11-keymap`
writes), so the password is typed as everywhere else.

The installer turns it on (without disk encryption: with it, the disk's
password is the login). On a system with another login manager (SDDM,
GDM…), turning it on takes its place from the next start; turning it off
leaves the text login.

Its files are system files: `mazapan apply --system` writes them, and a
new theme reaches the login screen on the next `mazapan apply --system`.

With an encrypted disk (the installer's default), its password is typed
once, as the computer starts: "Straight in at start" then logs that
account in by itself, once each start, and the same password opens the
keyring (pam_fde_boot_pw, from Mazapán's own repository). After logging
out, or with the setting off, it's the login screen as always.
