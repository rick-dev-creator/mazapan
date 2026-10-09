# Roadmap

Where Mazapan stands, what comes next, and what's waiting. How each part
was decided, built, tested and audited is in [history.md](history.md).

**Today (2026-10-09):** Mazapan runs on a real machine (an MSI Vector
A16 HX), mazapan.dev is up, the ISO and the signed package repository
are published, and releases come out as a routine (0.5.0 is the latest).
The plugin registry is open, with eight plugins. Not yet: 1.0, which
waits on the first three items below.

## Next

1. **Knowing it works out there, without spying.**
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
   - "Report this bug" from a crash (crash-watch): the
     issue shown before it's sent, duplicates looked for first, sent only
     with a yes.
2. **Known as a distribution, and upstream told.** DistroWatch's
   submission and the ArchWiki's list of Arch-based distributions, Arch's
   rules kept (its name and logo not used as if official; support on
   Mazapan's own channels). Bugs found here reported where they belong:
   Quickshell 0.3 (a kept window capture brings the shell down after a
   screen recording) and Hyprland 0.56 (a Lua timer stopped mid-countdown
   never fires again). For 1.0 what counts is that they're submitted
   and reported; when they're listed or fixed is up to others.
3. **VS Code ready for C#.** Microsoft's VS Code is already in the .NET
   profile (dev-dotnet); what's left is the C# Dev Kit and its extensions
   installed with it, and tried: F5 runs a backend and Aspire on Linux.
4. **1.0.** Once 1–3 are done (blocks E and F in the road below):
   tagged `v1.0.0`, from the stable channel.

## Plugins that make a difference

What comes next is mostly plugins: each one a goal someone has, reached
without a terminal. Built-in when nearly everyone needs it and it works on
any hardware; from the community (their own repository, in the registry)
when it depends on a brand, a service or a taste. Mazapan makes the first
community ones itself, as it did with Markets, Pomodoro, Now Playing,
Playback, Radio, Dock, Ports and News, so the registry starts with
plugins worth installing. They don't hold up 1.0: they're made alongside
Next and after it, in the order below.

### From the community

Simplest first:

1. **A wallpaper that moves.** A looping video as the wallpaper, muted,
   paused while a window covers it or on battery; part of a theme like any
   picture.
2. **The services I work in.** Tasks, issues and notifications from one
   service each (Todoist, Linear, GitHub…) in the bar or the palette, each
   signed in to its own way.
3. **My passwords at hand.** Bitwarden from the palette: search, copy a
   password or a code, the vault locked with the session; nothing kept
   outside the system's keyring.
4. **My home.** Home Assistant's lights, switches and scenes in the bar or
   the Control Center, and what its sensors say.
5. **The phone next to me.** Its screen in a window (scrcpy), the
   clipboard, files and notifications both ways (KDE Connect).
6. **My devices working.** One plugin per brand, where only its own tools
   reach: keyboard lighting, Logitech mice (Solaar), Razer (OpenRazer),
   headphones' battery, MSI laptops' fans and performance modes (msi-ec);
   each tried on the device itself. A charge limit isn't one of them: the
   battery plugin below does it for every brand.

### Built-in

Simplest first:

1. **How the machine is doing.** CPU, GPU, memory, temperatures and fans
   in the bar; a word when something runs hot or memory runs out, with
   what's using it, and stopping it from there.
2. **A VPN with one click.** WireGuard and OpenVPN through NetworkManager,
   a config file dropped in to add one, a switch in the Control Center,
   what's connected said in the bar.
3. **The camera before a call.** Any webcam's brightness, focus,
   exposure and zoom with a live preview, kept per camera and set again
   when it's plugged in.
4. **Where my time goes.** Time per app, today and over the weeks, kept
   on this computer only; a limit for an app if you want one.
5. **A battery that lasts years.** A charge limit (80 %, or full for a
   trip) where the kernel offers one (`charge_control_end_threshold`:
   ThinkPad, ASUS, Framework, Dell, Huawei, Samsung, MSI through
   msi-ec…), kept across restarts, and the battery's health over time,
   not only today's.
6. **Sound the way I want it.** Every output and input, more than one
   output at once, the codec of Bluetooth headphones, each app's volume
   and where it plays, and an equalizer with presets (PipeWire and
   WirePlumber).

## Road to 1.0

| Block | What it takes | State |
|---|---|---|
| A. It updates itself | Its own signed repository with channels, real versions, the updater | Done |
| B. Safe to install | Encrypted by default, a recovery key, one password, locked before sleep, the firewall | Done |
| C. Tested | The release gate (unattended installs in a VM) | Done |
| | A real machine (MSI Vector A16 HX) | Done |
| D. Published | License (MIT), the GitHub repository, a README for every plugin | Done |
| | The ISO, its checksum and signature; the repository hosted; mazapan.dev | Done |
| E. Known out there | Counting installs; hardware and errors only with a yes; "Report this bug" | To do |
| | DistroWatch and the ArchWiki submitted; the Quickshell and Hyprland bugs reported | To do |
| F. Ready for .NET work | VS Code with the C# Dev Kit, tried with a backend and Aspire | To do |

## Done

| Area | What's there |
|---|---|
| Updates | A preview with Arch's news, checks before and after, rolled back on its own; the bar and a panel, Flatpak, firmware and plugins too, downloaded ahead |
| History | Every change on a timeline, undone; checkpoints anyone understands, started from the boot menu, kept or restored |
| Install | The ISO with a graphical installer (any language), offline, unattended (`cidata`), welcome and profiles |
| Apps | A catalog with profiles (development, .NET, mobile, gaming, creative, office, retro…), languages and databases in one click, containers (Podman or Docker) |
| Agents | Every agent and account found, limits and the next account taken, live sessions in the bar, a dashboard, MCP with previews and approval; Mazapan, the agent: a pixel-art character you ask by text or voice (SUPER + SHIFT + A) that makes the changes you ask for with your approval, searches the web, and only answers about what isn't yours |
| Desktop | The palette, monitors with profiles, themes (and one from any picture), wallpapers, modes, notifications, capture with OCR and recording, sharing |
| Tiling | Niri's way on Hyprland's scrolling layout: columns, workspaces as a strip, the overview |
| Control Center | One pill on the bar's right, grown into one panel: what needs you, the switches, sound, music, agents, power |
| Settings | Keyboard, mouse, fonts, keys, default apps, language, password, the Security page |
| Learn | Lessons recorded from the real moves, tried for real and ticked off; every key while SUPER is held |
| Security | Encryption, the firewall, privacy dots, app permissions, fingerprint; audited (see [security.md](security.md)) |
| Plugins | Dependencies, approved capabilities, catalogs, the Plugins panel, tools for authors; hardware fixes offered only where they apply |
| Published | mazapan.dev, the ISO with its checksum and signature, the signed repository at repo.mazapan.dev, releases on their channels; the plugin registry, with eight plugins: Markets, Pomodoro, Now Playing (what's playing, anywhere), Playback (the same in Mazapan's look), Radio (live stations), Dock (your apps and their windows), Ports (what's listening, and stopping it) and News (headlines by country, language and topic) |
| Look | The login, boot menu and boot splash in the theme; GTK, Qt, browsers, editors and terminals themed |

## Waiting

After 1.0, roughly in this order:

- **Security:** Secure Boot (sbctl, signed images), security keys (FIDO2),
  TPM later.
- **For people at work:** backups of your files (as Time Machine),
  printers and scanners said when plugged in, files found from the
  palette, the calendar and the next meeting in the bar.
- **Less friction:** "where do you come from?" in the welcome (macOS- or
  Windows-like keys and windows), Alt+Tab with previews, Quick Look, a
  right click on the desktop, the scale from the screen's density.
- **Robustness:** an LTS kernel to fall back on, systemd-oomd, the disk's
  health, btrfs scrub, security advisories (arch-audit),
  reinstall keeping your files, a pinned Arch snapshot.
- **The same desktop anywhere:** config and plugins synced through git,
  what's per machine kept apart.
- **Tiling, more of Niri:** workspaces that go back to their screen, tabs
  in a column, widths per app, the overview by keyboard and gestures.
- **Windows apps:** a Windows VM with its apps as windows of their own
  (Office, Adobe, Visual Studio), as an optional plugin.
- **The store:** ratings, reviews and developers' answers on top of the
  registry (mazapan-store, ASP.NET Core and PostgreSQL), once there are
  more people making plugins than Mazapan itself. Its rules are already
  written: [store.md](store.md).
- **Plugins:** graphical plugins in .NET (Avalonia) with the theme and
  translations; a "customize" button, signed tags, screenshots in
  catalogs.
- **Smaller follow-ups:** themes for Electron apps and Qt 5, light and
  dark at sunset; the palette remembering what you pick; a capture across
  two screens, blur as a tool; per-app notification sounds; "ask an
  agent" offered when a check fails; the Ask card's buttons (Undo, Copy)
  by keyboard too, and an agent's approval card starting on "Don't allow";
  HDR; more hardware fixes (T2 Macs, thermald and lpmd rules).

## Dropped

- **Workspace sessions:** Hyprland's workspaces already do it.
- **The CLI in your language:** the CLI stays English; everything with a
  window is translated.
- **Chromium's accent through a system policy:** it needs root on every
  theme change.
