# Changelog

What changed in each release, newest first. The updates panel shows what's
new since the version a system has. Changes go under "Unreleased" until a
release (a tag `vX.Y.Z`) names them.

## Unreleased

- Mazapán updates itself: its own signed repository, in two channels
  (stable, and edge with every release first): `mazapan channel` says which
  one and switches, `mazapan version` says the version.
- Updates in a few steps, each with its ✓, from the terminal or the panel:
  room and power checked and the machine kept awake, the keyrings first,
  a failed initramfs rolled back, what needs a restart offered, what's new
  in Mazapán shown. A new Mazapán finishes the update it came in.
- The disk encrypted by default, with a recovery key (shown as text and a
  code to photograph) and one password: typed as the computer starts, it
  logs in and opens the keyring.
- Locked before it sleeps: the lid never opens on an unlocked desktop.
- The firewall on: nothing comes in that wasn't asked for.
- Unattended installs: a drive labeled cidata with the installer's answers
  (mazapan.json) installs by itself.
- The menu: SUPER + Space with nothing typed shows a tile for each place
  (Apps, Updates, Settings, Theme…), the power row and the open windows;
  typing finds apps that aren't installed too, to install them.
- The password changed in one place (Settings › Security, or `mazapan
  password`): the disk's, the account's and the keyring's together.
- Privacy dots in the bar while the microphone, the camera or the screen
  is in use.
- Hibernation on laptops: nothing lost when the battery dies.
- Settings › Text: the fonts (each shown in itself) and the text's size,
  over the theme's.
- Settings › Keys: every keybinding in one list, changed by pressing the
  new keys (one already taken is said).
- Updates: firmware (fwupd) and plugin updates in the same panel; updates
  downloaded ahead in the background, on power and unmetered only.
- The boot menu and the boot splash (with the disk's password) in the
  theme's colors.
- Wi-Fi shared as a QR code, and a speed test, from the network card.
- Extras, off until turned on: reminders (a bell in the bar), a crash
  watcher that offers to ask an agent, and a screensaver in the theme.
- The Apps menu takes catalogs from others (`app_catalogs` in config.toml).
- A keyboard picked in Settings is tried first: the one before comes back
  by itself unless it's kept.
- What each Flatpak app may reach (the internet, sound and microphone,
  devices, your files, Bluetooth), switched in Apps › Permissions or with
  `mazapan apps permit`.
- What a Flatpak app asked for through the system (the camera, the
  location…), answered again or forgotten, in Apps › Permissions.
- An app asks before it sees the screen; the desktop's own tools don't.
- Dictation, on the computer itself: speak, and it's typed where you are.
- Capture: what a QR code holds, copied as a secret (never shown nor kept
  in the clipboard history); text read in your language.
- Every built-in plugin has its page in the Plugins panel.
- Installing an app works before a repository was ever fetched (installed
  offline, or Mazapán's repository newly added).
- Fixed after an audit: the privacy dots no longer keep a processor busy,
  and no app name can hide them; changing the password checks the current
  one first; a plugin that can't be read keeps its files; hibernation only
  where its swap file fits; updates downloaded ahead in a folder of root's
  own; the text size slider applies; Wi-Fi codes right for any name or
  password; the boot menu never stops grub.cfg from being written.
- A plugin that can't be read is left out and said; the rest of the
  desktop still applies.
