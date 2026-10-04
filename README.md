<h1 align="center">
  <img src="assets/mazapan-app.svg" width="112" alt=""><br>
  Mazapan
</h1>

<p align="center">
  A desktop on plain Arch Linux: Hyprland and Quickshell, one theme across
  the whole OS, checkpoints you can boot into, and everything a plugin.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Arch_Linux-1793D1?style=for-the-badge&logo=archlinux&logoColor=white&labelColor=101418" alt="Arch Linux">
  <img src="https://img.shields.io/badge/Hyprland-58E1FF?style=for-the-badge&logo=hyprland&logoColor=101418&labelColor=101418" alt="Hyprland">
  <img src="https://img.shields.io/badge/Quickshell-8A6FDF?style=for-the-badge&labelColor=101418" alt="Quickshell">
  <img src="https://img.shields.io/badge/.NET_10_AOT-512BD4?style=for-the-badge&logo=dotnet&logoColor=white&labelColor=101418" alt=".NET 10 AOT">
  <img src="https://img.shields.io/github/license/rick-dev-creator/mazapan?style=for-the-badge&labelColor=101418&color=C9A15B" alt="MIT">
</p>

<p align="center">
  <a href="#features">Features</a> ·
  <a href="#screenshots">Screenshots</a> ·
  <a href="#install">Install</a> ·
  <a href="docs/development.md">Development</a> ·
  <a href="docs/plugin-api.md">Plugins</a> ·
  <a href="docs/roadmap.md">Roadmap</a>
</p>

<p align="center"><img src="docs/media/themes.gif" alt="Changing the theme of the whole desktop, then the overview"></p>

<p align="center"><sub>One command changes everything: bar, terminals, editors, GTK and Qt apps, browsers, the lock and login screens, GRUB.</sub></p>

> **A personal project, shared as it is.** Mazapan is one person's desktop,
> made public in case it helps someone else. It comes with no warranty of
> any kind (see [LICENSE](LICENSE)): installing it erases the disk you pick,
> so back up what matters first, and read [docs/first-install.md](docs/first-install.md)
> and [docs/security.md](docs/security.md) before trying it on a real machine.

<p align="center"><img src="docs/media/desktop.webp" alt="The desktop: btop and Neovim side by side in columns"></p>

## Features

Arch owns the critical parts (kernel, packages, updates); Mazapan is the
layer on top, written as 100 plugins over one small core, `mazapan`, a single
native binary. Every change it makes is previewed, written only where it
may, and undoable.

### Install and security
- A **graphical installer** from a live desktop (and a text one for when
  graphics fail), in English, Spanish, Portuguese, French and German.
- **Full-disk encryption** by default (LUKS2, argon2id), one password for the
  disk, the account and the keyring, plus a **recovery key** shown once as
  text and QR.
- Firewall denying everything in, SSH only with keys, polkit asking every
  time; what's protected and what isn't is written down in
  [docs/security.md](docs/security.md).
- **Hardware fixes** chosen for the machine it runs on (27 `hw-*` plugins):
  NVIDIA (hybrid laptops included), AMD and Intel graphics, ASUS ROG, Framework,
  Surface, Apple, fingerprint readers, Wi-Fi quirks.

### Updates and checkpoints
- `mazapan update`: Arch news first, then each step with its ✓, then the
  health checks; **rolled back on its own** when a check fails.
- **Checkpoints**: a snapshot before every package change, listed in the
  boot menu. Boot into one, and if it works, keep it with one click; if it
  doesn't, "What broke?" hands the diagnosis to an agent.
- Your own signed repository for Mazapan, stable and edge channels,
  downloads ahead of time while plugged in.

### Desktop
- **Columns tiling** borrowed from Niri: a scrolling strip per workspace,
  widths that cycle, touchpad gestures, and an **overview** of every
  workspace (`SUPER + Tab`).
- A **command palette** (`SUPER + Space`) for apps, windows and every
  plugin's actions, each showing the command it runs.
- **Settings** (`SUPER + ,`), monitors with live thumbnails and profiles
  (`SUPER + SHIFT + M`), notifications with Do Not Disturb, an OSD, night
  light, idle and hibernate, modes (the whole desktop switched at once, by
  hand, schedule or screen).
- Capture (`Print`): region or window, copy, save, OCR, annotate, record.
- Clipboard history, emoji picker, color picker, offline dictation
  (whisper.cpp), reminders, Chinese/Japanese/Korean input, privacy dots.
- **History** of every change to the desktop, in words, each with its undo.

### Themes
- **One theme for the whole OS**: 16 theme targets, from Hyprland and the
  bar to Firefox, Chromium, VS Code, Neovim, Obsidian, GTK, Qt, Plymouth
  and GRUB.
- A picker (`SUPER + SHIFT + T`) that previews each theme live on your
  desktop; any accent color; **a whole theme from a picture**, with its
  contrast checked.

### Apps
- **Apps by what you'll do** (`SUPER + ALT + A`): Development, .NET, Mobile
  (Expo), Gaming, Retro, Creative, Office, Streaming… one button, said
  first, undoable.
- Web apps as apps of their own, default apps, Podman (or Docker) and
  one-click databases for development.

### Gaming and retro
- Steam, Heroic, Lutris, GameMode, MangoHud and gamescope, with the right
  Vulkan drivers for NVIDIA, AMD or Intel; tearing allowed and idle held
  while a game runs, games on the discrete GPU of a hybrid laptop.
- **Emulators** (RetroArch, DuckStation, PCSX2, Dolphin…), a ROMs folder per
  console, a game mode for the bar and notifications.
- Seven **screensavers** of Mazapan's own: Mazapan, CRT, rain, stars, life,
  pipes, glow.

### Agents
- Claude Code, Codex and others in the bar: which is working, which waits
  for you, each account's limits and spend.
- `mazapan mcp`: agents change the desktop through the same previewed,
  undoable steps a person does ([docs/agent-api.md](docs/agent-api.md)).
- "Ask an agent" on a crash, a failed check or a checkpoint, read-only.

### Sharing
- LocalSend to devices nearby, Taildrop to yours over Tailscale, from the
  palette or the file manager.

## Screenshots

<table>
  <tr>
    <td width="50%"><img src="docs/media/palette.webp" alt="Command palette"><p align="center"><b>Command palette</b></p></td>
    <td width="50%"><img src="docs/media/overview.webp" alt="Overview"><p align="center"><b>Overview</b> of every workspace</p></td>
  </tr>
  <tr>
    <td><img src="docs/media/theme-picker.webp" alt="Theme picker"><p align="center"><b>Theme picker</b>, previewed live</p></td>
    <td><img src="docs/media/apps.webp" alt="Apps by profile"><p align="center"><b>Apps</b> by what you'll do</p></td>
  </tr>
  <tr>
    <td><img src="docs/media/checkpoint.webp" alt="Booted into a checkpoint"><p align="center">Booted into a <b>checkpoint</b></p></td>
    <td><img src="docs/media/installer.webp" alt="Recovery key at the end of the install"><p align="center">The <b>installer</b>'s recovery key</p></td>
  </tr>
  <tr>
    <td><img src="docs/media/retro.webp" alt="A homebrew NES game in RetroArch"><p align="center"><b>Retro</b>: a homebrew NES game in RetroArch</p></td>
    <td><img src="docs/media/screensavers.webp" alt="Screensavers"><p align="center"><b>Screensavers</b>: Mazapan, CRT, pipes, rain</p></td>
  </tr>
  <tr>
    <td colspan="2"><img src="docs/media/settings.webp" alt="Settings" width="60%" align="center"><p align="center"><b>Settings</b></p></td>
  </tr>
</table>

## Install

There's no published ISO yet: build it from this repository in the dev VM
(`vm/vm iso`, see [docs/development.md](docs/development.md)), write it to
a USB stick, and boot it. Before a real machine, read
[docs/first-install.md](docs/first-install.md): Secure Boot off, graphics
on Hybrid, the recovery key written down.

Already on Arch with Hyprland? `mazapan` runs on its own too:

```sh
core/build test                  # needs the .NET 10 SDK and clang
mazapan apply --dry-run          # what it would write, nothing written
mazapan apply --theme phosphor   # write it; mazapan undo takes it back
```

## Documentation

| | |
|---|---|
| [docs/development.md](docs/development.md) | The `mazapan` command, the repository, the dev VM, each part of the desktop in detail |
| [docs/plugin-api.md](docs/plugin-api.md) | Writing a plugin: targets, templates, settings, health checks |
| [docs/agent-api.md](docs/agent-api.md) | Agents and MCP |
| [docs/first-install.md](docs/first-install.md) | The first install on real hardware |
| [docs/security.md](docs/security.md) | What's protected, and what isn't |
| [docs/roadmap.md](docs/roadmap.md) | Where it's going |

## License

[MIT](LICENSE). What Mazapan learned from others, and their notices, is in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md); among them Omarchy, whose
hardware fixes the `hw-*` plugins carry. The name "Mazapan" and its icon
aren't covered by the license: a fork is welcome under another name.
