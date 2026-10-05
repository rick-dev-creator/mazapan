# Roadmap

Where Mazapan stands, what comes next, and what's waiting. How each part
was decided, built, tested and audited is in [history.md](history.md).

**Today (2026-10-05):** everything a desktop needs is built and tried in
virtual machines, and the ISO passes the release gate (installed by
itself, encrypted and plain, every check green). Not yet: a real
machine, a published ISO, 1.0.

## Next

1. **The laptop.** An MSI Vector A16 HX (Ryzen 9 HX + NVIDIA Blackwell,
   MediaTek Wi-Fi) from the USB stick, following
   [first-install.md](first-install.md). What only real hardware shows:
   hybrid graphics, suspend and hibernate, Wi-Fi, Bluetooth, the battery
   and brightness in the Control Center, night light's warmth.
2. **The ISO published.** On Cloudflare R2 behind the official site, its
   checksum and signature in the GitHub release; the release key and
   the package repository hosted. On the way: `nvidia-open` instead of
   `nvidia-open-dkms` (~150 MB less).
3. **The time as one button.** The bar's center grows into a panel (the
   clock, the calendar, the weather by the hour, reminders), as the
   Control Center does on the right.
   [Concept](https://claude.ai/artifact/7Enp3ALZNNp1UAvFTaEjQG).
4. **VS Code ready for C#.** The C# Dev Kit and its extensions in the
   .NET profile, so F5 runs a backend and Aspire on Linux.
5. **1.0.** Once 1 and 2 are done: tagged `v1.0.0`, from the stable
   channel.

## Road to 1.0

| Block | What it takes | State |
|---|---|---|
| A. It updates itself | Its own signed repository with channels, real versions, the updater | Done |
| B. Safe to install | Encrypted by default, a recovery key, one password, locked before sleep, the firewall | Done |
| C. Tested | The release gate (unattended installs in a VM) | Done |
| | A real machine | **Next** (1) |
| D. Published | License (MIT), the GitHub repository, a README for every plugin | Done |
| | The ISO, its checksum and signature; the repository hosted | **Next** (2) |

## Done

| Area | What's there |
|---|---|
| Updates | A preview with Arch's news, checks before and after, rolled back on its own; the bar and a panel, Flatpak, firmware and plugins too, downloaded ahead |
| History | Every change on a timeline, undone; checkpoints anyone understands, started from the boot menu, kept or restored |
| Install | The ISO with a graphical installer (any language), offline, unattended (`cidata`), welcome and profiles |
| Apps | A catalog with profiles (development, .NET, mobile, gaming, creative, office, retro…), languages and databases in one click, containers (Podman or Docker) |
| Agents | Every agent and account found, limits and the next account taken, live sessions in the bar, a dashboard, MCP with previews and approval |
| Desktop | The palette, monitors with profiles, themes (and one from any picture), wallpapers, modes, notifications, capture with OCR and recording, sharing |
| Tiling | Niri's way on Hyprland's scrolling layout: columns, workspaces as a strip, the overview |
| Control Center | One pill on the bar's right, grown into one panel: what needs you, the switches, sound, music, agents, power |
| Settings | Keyboard, mouse, fonts, keys, default apps, language, password, the Security page |
| Learn | Lessons recorded from the real moves, tried for real and ticked off; every key while SUPER is held |
| Security | Encryption, the firewall, privacy dots, app permissions, fingerprint; audited (see [security.md](security.md)) |
| Plugins | Dependencies, approved capabilities, catalogs, the Plugins panel, tools for authors; hardware fixes offered only where they apply |
| Look | The login, boot menu and boot splash in the theme; GTK, Qt, browsers, editors and terminals themed |

## Waiting

After 1.0, roughly in this order:

- **Security:** Secure Boot (sbctl, signed images), security keys (FIDO2),
  TPM later.
- **For people at work:** backups of your files (as Time Machine),
  printers and scanners said when plugged in, files found from the
  palette, the calendar and the next meeting in the bar, the phone
  linked (KDE Connect).
- **Less friction:** "where do you come from?" in the welcome (macOS- or
  Windows-like keys and windows), a dock or taskbar with Alt+Tab
  previews, Quick Look, a right click on the desktop, the scale from the
  screen's density.
- **Robustness:** an LTS kernel to fall back on, systemd-oomd, the disk's
  and battery's health, btrfs scrub, security advisories (arch-audit),
  reinstall keeping your files, "report this bug" from a crash, a pinned
  Arch snapshot.
- **The same desktop anywhere:** config and plugins synced through git,
  what's per machine kept apart.
- **Tiling, more of Niri:** workspaces that go back to their screen, tabs
  in a column, widths per app, the overview by keyboard and gestures.
- **Windows apps:** a Windows VM with its apps as windows of their own
  (Office, Adobe, Visual Studio), as an optional plugin.
- **Plugins:** graphical plugins in .NET (Avalonia) with the theme and
  translations; a "customize" button, signed tags, screenshots in
  catalogs.
- **Smaller follow-ups:** themes for Electron apps and Qt 5, light and
  dark at sunset; the palette remembering what you pick; a capture across
  two screens, blur as a tool; per-app notification sounds; "ask an
  agent" offered when a check fails; HDR; more hardware fixes (T2 Macs,
  thermald and lpmd rules).

## Dropped

- **Workspace sessions:** Hyprland's workspaces already do it.
- **The CLI in your language:** the CLI stays English; everything with a
  window is translated.
- **Chromium's accent through a system policy:** it needs root on every
  theme change.
