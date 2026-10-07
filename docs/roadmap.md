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
2. **mazapan.dev and the ISO published.** The domain on Cloudflare, the
   site on Cloudflare Pages (what it is, downloads with how to verify them,
   release notes, the manual and Learn on the web, hardware that works,
   privacy, contributing); `os-release`'s URLs pointing there. The ISOs and
   the signed package repository on Cloudflare R2 (no charge per
   download), a torrent with R2 as its web seed, SourceForge as a mirror;
   later Fastly's or OSUOSL's programs for free software. On the way:
   `nvidia-open` instead of `nvidia-open-dkms` (~150 MB less).
3. **Releases, as a routine.** Mazapan's versions (`vX.Y.Z`, the stable,
   edge and dev channels), each minor one with a name to remember it by
   (see docs/versioning.md),
   apart from the ISO's: an ISO for each release
   and a fresh one each month (`mazapan-1.0.0-2026.11-x86_64.iso`), each
   with its SHA-256, a signature by the release key and a torrent; built
   here, through the release gate (encrypted and plain), signed, uploaded,
   announced. The last two or three on R2, older ones by torrent; the
   repository never pruned of what installed systems need.
4. **Knowing it works out there, without spying.**
   - How many: Fedora's "countme": once a week the update check says its
     version and rough age, no identifier; counted from the repository's
     logs, with the ISO's downloads.
   - Hardware and errors, **only if the person says yes** (a step in the
     welcome, off by default, with "see exactly what's sent"): the
     machine's model, CPU, GPU and driver, the hardware fixes it took; and
     signatures of what failed where Mazapan is in charge (an install, an
     update rolled back, first-login apps, doctor's checks, a crash of the
     shell or of mazapan). No serials, names or addresses; kept 90 days;
     the totals published (a hardware page on the site). A Cloudflare
     Worker with D1 to receive them.
   - "Report this bug" from a crash (crash-watch, as Omarchy does): the
     issue shown before it's sent, duplicates looked for first, sent only
     with a yes.
5. **Known as a distribution, and upstream told.** DistroWatch's
   submission and the ArchWiki's list of Arch-based distributions, Arch's
   rules kept (its name and logo not used as if official; support on
   Mazapan's own channels). Bugs found here reported where they belong:
   Quickshell 0.3 (a kept window capture brings the shell down after a
   screen recording) and Hyprland 0.56 (a Lua timer stopped mid-countdown
   never fires again).
6. **The time as one button.** The bar's center grows into a panel (the
   clock, the calendar, the weather by the hour, reminders), as the
   Control Center does on the right.
   [Concept](https://claude.ai/artifact/7Enp3ALZNNp1UAvFTaEjQG).
7. **VS Code ready for C#.** The C# Dev Kit and its extensions in the
   .NET profile, so F5 runs a backend and Aspire on Linux.
8. **1.0.** Once 1 and 2 are done: tagged `v1.0.0`, from the stable
   channel.

## Plugins that make a difference

What comes next is mostly plugins: each one a goal someone has, reached
without a terminal. Built-in when nearly everyone needs it and it works on
any hardware; from the community (their own repository, in the registry)
when it depends on a brand, a service or a taste. Mazapan makes the first
community ones itself, as it did with Markets and Pomodoro, so the registry
starts with plugins worth installing.

### From the community

Simplest first:

1. **What I follow.** Feeds and tickers, as Markets does: one each.
2. **What's running while I build.** The ports in use and which process
   holds each, the containers up, stopped with a click: next to
   dev-databases and dev-dotnet. The first one made from scratch for the
   registry.
3. **The services I work in.** Tasks, issues and notifications from one
   service each (Todoist, Linear, GitHub…) in the bar or the palette, each
   signed in to its own way.
4. **The phone next to me.** Its screen in a window (scrcpy), files and
   notifications both ways (KDE Connect).
5. **My devices working.** One plugin per brand, where only its own tools
   reach: keyboard lighting, Logitech mice (Solaar), Razer (OpenRazer),
   headphones' battery, MSI laptops' charge limit and fans (msi-ec); each
   tried on the device itself.

### Built-in

Simplest first:

6. **How the machine is doing.** CPU, GPU, memory, temperatures and fans
   in the bar; a word when something runs hot or memory runs out, with
   what's using it, and stopping it from there.
7. **A VPN with one click.** WireGuard and OpenVPN through NetworkManager,
   a config file dropped in to add one, a switch in the Control Center,
   what's connected said in the bar.
8. **A battery that lasts years.** A charge limit (80 %, or full for a
   trip) where the kernel offers one (`charge_control_end_threshold`:
   ThinkPad, ASUS, Framework, Dell, Huawei, Samsung…), kept across
   restarts, and the battery's health over time, not only today's.

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
  palette, the calendar and the next meeting in the bar.
- **Less friction:** "where do you come from?" in the welcome (macOS- or
  Windows-like keys and windows), a dock or taskbar with Alt+Tab
  previews, Quick Look, a right click on the desktop, the scale from the
  screen's density.
- **Robustness:** an LTS kernel to fall back on, systemd-oomd, the disk's
  health, btrfs scrub, security advisories (arch-audit),
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
