# Roadmap: what Omarchy has, and how to do it much better

Not a list of features to copy. Omarchy (4.0.4, 441 bash commands) already
ships most of these in some form, including a plugin system and an AI
agent for crashes; each item says what it does today and what "much better"
means here. Ordered by impact.

## Toward 1.0: a stable version anyone can install

Decided on 2026-10-03. Most of what a desktop needs is done (the
progress below); what's missing is what makes it a system someone can
install and keep: an installed Mazapán never updates itself today (the
installer takes the package from the ISO's own repository, and the
installed system has no `[mazapan]` repository), nothing protects the
disk, nothing has run on real hardware, and nothing is published. Until
1.0, work goes in this order; everything else waits.

**A. It updates itself** (blocks everything else)
1. Mazapán's own signed package repository: `mazapan` and a
   `mazapan-keyring` package (pacman-key, as archlinux-keyring), in
   channels: `stable`, `edge` (every release first), and `dev` (a
   checkout, the dev VM's). The installed system has it from the install
   on; `mazapan channel` switches. *Done* (2026-10-03) but for what's the
   person's: the release key (`pkg/keys`), where it's hosted
   (`pkg/repository.toml`), and uploading there.
2. Real versions: tags `vX.Y.Z` (not a commit count), `mazapan --version`,
   a changelog, and one command that makes a release (core, package,
   signature, repository database). *Done* (2026-10-03): `pkg/version`,
   `mazapan version`, `CHANGELOG.md`, `pkg/release` and `pkg/release
   promote`.
3. The updater, below. *Done* (2026-10-03).

Tested end to end in the VMs with a throwaway key and a `file://`
repository: a release to edge, promoted to stable; an ISO built with it;
installed (the [mazapan] section, the mirrorlist on stable, the key in
pacman's keyring, paccache.timer); then 0.1 → 0.2 → 0.3 → 0.4 from the
terminal and 0.4 → 0.5 from the panel (signatures checked, snapper's
pair, the new mazapan finishing the update, the panel's steps, "what's
new"); channels switched; a login applying a changed Mazapán. Learned:
a repository pacman can't reach stops every update, Arch's too, so it
has to be hosted somewhere that stays up; repo-add makes symlinks
(`mazapan.db`), which uploading to GitHub has to copy as files.

**B. Safe to install** (the essentials of 19)
4. The disk encrypted by default, a recovery key, the keymap in the
   initramfs, one password (19, part 1). Built (2026-10-03): the
   installer's switch starts on; at the end, a recovery key
   (systemd-cryptenroll) as text and a QR code, and the restart waits
   until the person says it's kept. One password: greetd goes straight in
   that start (the login plugin's `autologin`, on for an encrypted
   install), and the keyring opens with the disk's password through
   pam_fde_boot_pw (by greetd's author: greetd's autologin skips PAM's
   auth step, where pam_systemd_loadkey works; it injects it in the
   session step), packaged in Mazapán's own repository. Not Omarchy's
   keyring without a password any more. What it took, found in the VM:
   systemd's initramfs (archinstall makes the older `encrypt` one unless
   there's a security key: the post-install switches it to sd-encrypt and
   sd-vconsole, `rd.luks.name=`), the module before PAM's session include
   (its pam_keyinit makes the disk's password unreadable), and the login
   keyring made while installing, with the password (one made at the
   first login isn't on D-Bus until the next start: gnome-keyring #137).
   Tested from the ISO: the password typed once at start, the desktop
   straight in, the keyring open, a secret saved and read with no prompt,
   on the very first start.
5. Locked before it sleeps (19, part 2, its first half). Built
   (2026-10-03): hypridle holds the sleep until the lock screen is up
   (`inhibit_sleep = 3`).
6. The firewall on (19, part 4). Built (2026-10-03): plugin `firewall`
   (ufw: nothing in, everything out; mDNS and SSDP still), on from the
   installer; SSH let in (rate-limited) only when the install was given
   keys to be reached with. Docker's published ports go past it
   (ufw-docker is AUR only): said in its README.

Block B tested end to end (2026-10-03): an encrypted install from the
ISO, the recovery key on the installer's last screen, the first start
(one password, keyring open, firewall on, hypridle holding sleep), and
updates from the signed repository on it: one that broke the
configuration rolled back on its own, the next one went through.

**C. Tested**
7. Unattended installs (`cidata`), first as the release gate: every
   release installs itself in a VM, boots, and passes its checks before
   it's published. Built (2026-10-03): a drive labeled cidata (a USB
   stick, a VM's seed) with `mazapan.json` on it, the installer's own
   answers (`"disk": "auto"` takes the one disk there is; `"after"`:
   poweroff, reboot or none), installs with nobody there, as Omarchy's;
   the live desktop's installer shows how it goes; on a writable drive the
   recovery key and the log are left on it. `vm/gate [encrypted|plain]`
   is the gate: a fresh VM installs itself from the newest ISO, starts
   (the disk's password typed by QEMU), and passes there: every check in
   the session, no failed units, the keyring open with the one password;
   with `MAZAPAN_GATE_REPO`, an update from the release's repository too.
   Passing (2026-10-03), encrypted (with the update, 0.1.0 → 0.4.0) and
   plain. Found on the way: the live ISO ran out of memory installing on
   8 GB (archiso copied its 4 GB image to RAM: `copytoram=n` now), the
   unattended install waits for cloud-init (its SSH keys), and the web apps
   check failed on every fresh install (it asks for a browser only when
   there are web apps).
8. The live USB on real hardware (an RTX 4090 with an AMD iGPU, four
   screens), without installing; what fails, fixed.

**D. Published**
9. The ISO published, with its checksum and signature, and an install
   guide.
10. A license (the package says `unknown`).
11. The 34 built-in plugins without a README.

Then 1.0. After it, in this order: the rest of 18 (fonts, keybindings),
21 (the boot splash and menu in the theme), the rest of 19 (fingerprint,
the privacy dots, hibernation), the rest of 20 (firmware, downloading
ahead), 13, then 22–24.

Pending decisions (the person's): where the repository and the ISO are
hosted (GitHub Releases if the project is public, the Gitea's Arch
package registry if not), public or private, and the license. A pinned
Arch snapshot (Omarchy's stable mirror) waits until after 1.0: it needs
hosting and someone to move it forward, and the checks, rollback and
snapshots already cover a bad update.

### The updater: Omarchy's, as simple, and more

Omarchy's (`omarchy-update`, 4.0.4) is one confirmation and a short
sequence: free space checked, the package cache pruned, a snapshot, the
machine kept awake, the keyrings first (a stale `archlinux-keyring` is
the most common reason an Arch update fails), `pacman -Syu`, migrations,
AUR, orphans offered, the log searched for a failed initramfs, then a
reboot offered when the kernel changed or Hyprland's binary was replaced,
and the shell restarted. Its own repository has channels (stable, rc,
edge, dev), and stable also pins Arch itself to a tested date
(`stable-mirror.omarchy.org`).

Done (2026-10-03). Ours keeps that shape (one button or one command, one confirmation, a few
lines with a ✓ each) and what it already does better: the preview with
the Arch news that need a hand, checks before and after with an automatic
rollback without a reboot, the history, no migrations (an update is an
apply), the panel with pkexec, Flatpak apps.

```
Update Mazapán                          34 packages · reboot (kernel)
  ✓ Getting ready      free space, on power, kept awake
  ✓ Keys               archlinux-keyring, mazapan-keyring
  ✓ Snapshot           (snap-pac: bootable from the menu)
  ✓ Packages           Arch, Mazapán, Flatpak apps
  ✓ Configuration      mazapan apply
  ✓ Checks             one breaks → rolled back on its own
  Done. The kernel changed: [Reboot now] [Later]
```

New, from Omarchy: the free space, kept awake (systemd-inhibit), the
keyrings first, what needs a restart after (the kernel, a replaced
Hyprland, the shell restarted on its own). Better than it:
- A failed initramfs is a failed update: rolled back, and no reboot
  offered (Omarchy only warns).
- "What's new" in the panel, from the changelog, not a link.
- `pacman -Syu` by hand: each account remembers the Mazapán that last
  wrote its desktop, and a login with another one applies it (`mazapan
  apply --if-updated`; Omarchy runs its migrations at login too). No hook
  in pacman.
- When the update brings a new Mazapán, the new one writes the
  configuration, runs the checks and, if needed, rolls back (`update
  --continue`): its plugins may need its own code.
- Orphans offered, never removed alone; the cache keeps three versions
  (paccache.timer, weekly: rollbacks come from it).
- After 1.0: downloading ahead (on power, unmetered), firmware (fwupd),
  a pinned Arch snapshot.

## Progress

| # | Item | State |
|---|------|-------|
| — | Toward 1.0, C7: unattended installs, the release gate | **Done** (2026-10-03): `cidata` + mazapan.json installs by itself; `vm/gate` passes encrypted and plain. C8 (the live USB on real hardware) needs the person |
| — | Toward 1.0, B: safe to install | **Done** (2026-10-03): encrypted by default with a recovery key, one password (keyring included), locked before sleep, firewall on |
| — | Toward 1.0, A: it updates itself | **Done** (2026-10-03): its own signed repository with channels, versions and releases, the updater after Omarchy's. Pending, the person's: the release key, hosting, uploading |
| 1 | Updates you can trust | **Done** (2026-09-28); follow-ups listed below |
| 2 | Monitors | **Done** (2026-09-28); follow-ups listed below |
| 3 | One command palette | **Done** (2026-09-28): first version; follow-ups listed below |
| 4 | Themes | **Done** (2026-09-28); follow-ups listed below |
| 5 | Hardware | **Done** (2026-09-29); follow-ups listed below |
| 6 | An agent-native system | **Done** (2026-09-29); follow-ups listed below |
| 7 | Plugins | **Done** (2026-09-29); follow-ups listed below |
| 8 | Capture | **Done** (2026-09-29); follow-ups listed below |
| — | Plugin ecosystem (catalogs, Plugins panel, author tools) | **Done** (2026-09-29), under 7 |
| — | Notifications | **Done** (2026-09-29) |
| — | The name: Mazapán, and its icon | **Done** (2026-09-30): was myarch; a bitten mazapán in pixel art, in the bar, the installer and the login screen |
| 9 | Wallpapers, and a theme from any picture | **Done** (2026-09-29) |
| 10 | The essentials still missing | **Done** (2026-09-29) |
| 11 | Modes | **Done** (2026-09-29) |
| 12 | The desktop's history, visible | **Done** (2026-09-29) |
| 13 | The same desktop anywhere | Planned |
| 14 | Workspace sessions | Dropped (2026-09-30): Hyprland's workspaces are enough |
| 15 | The CLI in your language | Dropped (2026-09-30): the CLI stays in English; everything graphical is localized |
| 16 | Installation and first boot | **Done** (2026-09-30): catalog (17), welcome, the ISO with its graphical installer (any language, time zone, keyboard), snapshots from the install on; what any machine needs (its hardware's drivers chosen live and installed offline, touchpad, keyring, default apps, printing, input methods); audited. Pending: Mazapán's own signed package repository and publishing the ISO (both need a hosting and signing decision), unattended installs (`cidata`), real hardware |
| 17 | Apps: install and remove | **Done** (2026-09-29): catalog, `mazapan apps`, the Apps menu with profiles (several at once); Flatpak apps show without a new login (2026-09-30); audited. Pending: catalogs from others |
| 18 | Settings with a face | In progress: the Settings panel (keyboard, touchpad and mouse, default apps, language and time zone) done (2026-09-30); fonts, keybindings, preview to do |
| 19 | Security | Planned, researched (2026-09-30): what a macOS user expects, each part copied from established practice; order below |
| 20 | Updates, visible | **Done** (2026-09-30): the bar says when there are, a panel shows them (news, restart), updated with a click (pkexec), Flatpak apps too. Pending: firmware, downloading ahead |
| 21 | Boot and login in the theme | In progress: the login screen (plugin `login`, greetd) done; the boot splash and menu to do |
| 22 | Sharing | Planned |
| 23 | More capture | Planned |
| 24 | Extras | Planned |

Part two (items 9–15, 2026-09-29): what's still missing next to Omarchy,
and what would set this apart from it. 9–12 done; 13 left; 14 and 15
dropped.

Part three (items 16–24, 2026-09-29): what Omarchy (4.0 "Quattro", read
from its scripts) has that this still doesn't, each done better. Without
16 nobody else can use this, so it came first; 16, 17 and 20 done.
Next: the road to 1.0, above.

## The core: Go to C#

On 2026-09-29 the core moved from Go to C# (.NET 10, Native AOT: one
12.7 MB native binary, no runtime, ~4 ms to start), and plugin templates
from Go's text/template to Scriban. Verified byte for byte against the Go
version: every generated file for 18 theme × accent × language × settings
combinations, `themes --json`, `coverage --json`, `plugins`, and a full
`apply`; state written by Go (owned.json, update records) reads as it was.
Audited in three parts (25 findings, fixed): the command language behind
capabilities is an allowlist now (shared functions written as code, `this`,
`object.eval`, loops or capture could run what the approval didn't show);
templates get fresh data (one could change another plugin's actions); a
template path could read any file (`../../.ssh/…`); symlink loops and
self-referencing JSON crashed; reloads with a lingering child hung; an
unexpected error stopped a rollback half way; non-UTF-8 shared files were
rewritten with their bytes changed; config.toml could be written in a form
that didn't read back; duplicated TOML keys silently won.

## 1. Updates you can trust

**Omarchy today.** 106 migration scripts run once, in order. `refresh-config`
copies a shipped config over yours (with a backup). Updates take a snapper
snapshot first and grep the log for known failures afterwards.

**Much better.** *Done* — `mazapan update`, `doctor`, `history`, `rollback`:
- No config migrations: config is declarative and `mazapan` knows which
  files it owns, so an update is just a new `apply`.
- A preview before anything changes: packages (★ the ones the desktop
  depends on), Arch news since the last update (flagging manual
  interventions), generated files. One confirmation; pacman doesn't ask
  again, and isn't run at all when only generated files change.
- Health checks declared by plugins (`[[checks]]`), run before and after
  the update: only a check that passed before and still fails when tried
  again a few seconds later counts as the update's doing. They cover:
  Hyprland accepts the config (the new binary, without starting), the bar
  loads on the new Quickshell (restarted once, never duplicated), network,
  sound and Bluetooth when present. Checks that need the graphical session
  are skipped, not failed, from a TTY or SSH.
- An automatic rollback when one fails, with no reboot: the previous
  versions of exactly the changed packages, from pacman's cache or else the
  Arch Linux Archive, in one transaction (replaced packages resolved); the
  generated files as they were, hand edits backed up, never lost, and
  files a later `mazapan apply` rewrote left alone.
- A record from before anything changes, so an interrupted update is still
  in the history and can be rolled back. Rollbacks go newest first: an old
  update can't be undone under newer ones.
- Audited four times (33 findings, all fixed) and tested end to end in
  the dev VM: success, breakage with auto-rollback, archive fallback,
  manual rollback over a hand edit, interrupted update, updating the bar
  itself, rollback order, hand-edited conflict, config-only update, a
  check already failing before the update (and one breaking alongside
  it: rolled back, and older updates can still be rolled back), rollback
  after a later apply. Not exercised
  in the VM: rolling back an update that replaced one package with another.

**Follow-ups.** btrfs snapshots as the safety net for what a package
rollback can't fix (an unbootable system): now part of 12; AUR packages; an "updates
available" widget in the bar; reset one plugin's files; the CLI's own text
localized like the plugins.

## 2. Monitors

**Omarchy today.** About 15 scripts: clamshell, external active, scaling,
mirror, recover the internal monitor, a watcher for removed monitors.

**Much better.** *Done* — plugin `monitors`:
- Profiles matched by EDID (`~/.config/mazapan/monitors.json`), applied by
  a Hyprland Lua engine at startup and on every hotplug: mode, position,
  scale, rotation, adaptive sync (VRR), 10-bit color, mirroring, screens
  off, and which workspaces live on each screen. Screens without an EDID,
  or identical twins, fall back to the connector.
- When no profile matches, screens a profile turned off or mirrored show
  their own desktop again, so undocking can't leave you without a screen;
  a notice says how to save a profile for the new set.
- A manager in Quickshell (`SUPER + SHIFT + M`, or the screen icon in the
  bar, which shows how many screens there are), sized to the screen it
  opens on: live thumbnails of every screen, dragged into place (they snap
  to each other's edges, never overlap, and glide into position); every
  physical screen shows its name in large letters while it's open. Mode,
  scale, rotation, on/off, VRR, 10-bit, mirror (no chains) and workspaces;
  "Try" goes back on its own after 15 s unless kept, and reads back what
  each screen accepted: what it can't do is switched off and reported
  instead of pretending. "Save profile". The shell gained panels for it
  (plugin windows outside the bar, opened over IPC).
- The engine knows every screen that's plugged in, not just the ones
  Hyprland lists: one turned off or mirrored (by a profile, the manager
  or anything else) still counts, remembered across config reloads and
  forgotten when the kernel says it's unplugged. A profile that leaves no
  screen visible is never applied, and the manager won't try or save one.
- Chosen over reusing nwg-displays or wdisplays: they look out of place
  and don't adapt to the screen.
- The desk's 4-screen layout from Omarchy turned into the first profile.
- Tested in the dev VM with Hyprland's headless outputs as hotplugged
  screens: arrange, try, auto-revert, keep, save, unplug/replug after a
  reload (the engine re-applies), a screen turned off by a profile, the
  undock case, mirror on/off from the manager, unplugging a mirror's
  source and plugging it back, 10-bit on a screen that takes it and on one
  that doesn't (virtio: reported, switched off), a screen turned off from
  the manager surviving a reload and coming back on when undocked, a
  broken monitors.json left untouched, no layout without a visible
  screen. VRR can't be exercised in a VM.
- Audited once (11 findings, all fixed).

**Follow-ups.** HDR (`cm = "hdr"`, accepted by Hyprland but untested
without an HDR screen). Notifications: *done* (2026-09-29), the
`notifications` plugin, below.

## 3. One command palette

**Omarchy today.** 14 separate menus (keybindings, clipboard, emoji, share,
capture…); the keybinding list is parsed out of the config.

**Much better.** *Done* — plugin `palette` (`SUPER + Space`, or the Arch
logo at the start of the bar):
- One palette that searches open windows, installed apps, and every
  plugin's actions and keybindings at once (fuzzy; letters scattered
  across unrelated words don't count; `>` for actions and keys only).
- Every result shows the command it runs: the "learn to be the hacker"
  mode — you start by feeling like one and end up being one. `ctrl+c`
  copies it. Keybindings that call Lua are exposed as `hyprctl eval`
  commands, so the key and the command do exactly the same.
- Keybindings listed from the plugins that define them (`[[actions]]` in
  plugin.toml, rendered and handed to every template as `.Actions`), so
  the list is always right. Commands that ask or print (`mazapan update`,
  `doctor`, `rollback`…) open in a terminal that stays.
- Audited once (9 findings, all fixed). Tested in the dev VM: open with
  the key, search, run an action, launch a terminal app, launch
  an app, run a terminal action, copy a command, close with SUPER + Q.

**Follow-ups.** Files (plocate/fd) and settings (needs `mazapan set`);
remembering what you pick often; plugin actions with arguments; a check
that every `hl.bind` has its action.

## 4. Themes

**Omarchy today.** 34 theme commands and one `theme-set-*` per app; palettes
centered on the 16 ANSI colors. Its own theme preview shows the file
manager unthemed.

**Much better.**
- Semantic tokens that include shape and motion. *Done.*
- *Done* — a theme picker (plugin `themes`, `SUPER + SHIFT + T`): every
  theme as a small desktop drawn from its own tokens (bar, a terminal in
  its ANSI colors, a window with the selection and buttons), so any
  theme, yours too, has an exact preview; no screenshots to keep up to
  date. Moving through them previews each one live on the real desktop:
  the bar, panels, wallpaper and window borders morph to it (a 320 ms
  color transition, no flash), and terminals already open take its colors
  (escape sequences, like pywal); ↵ applies it everywhere, esc goes back
  to yours.
- *Done* — effects in the theme: see-through terminals (`terminal_opacity`)
  with the wallpaper blurred behind them, and a wallpaper drawn from the
  theme's tokens (plugin `wallpaper`): a faint perspective floor in the
  accent over a gradient, CRT scanlines, for dark themes; graph paper for
  light ones; or the theme's own image.
- *Done* — the accent is yours to pick in any theme: the theme's own or
  the ones it suggests (`accents` in theme.toml, or any #rrggbb with
  `mazapan apply --accent`). The other accent tokens (text on it, as text,
  a deep tint, the selection) are derived in OKLab with contrast kept.
- *Done* — contrast checked for every theme and accent (`mazapan themes`,
  and in the picker): the pairs plugins rely on against WCAG's minimums.
  It caught Gruvbox's red at 4.3:1 on its background. Derived accents keep
  every pair: tested over a grid of 216 accents in every bundled theme.
- Audited once (8 findings, all fixed); a demo recorded in the dev VM.
- Three more themes: Amber (the 80s amber CRT), Gruvbox, and Paper (light).
- *Done* — coverage: `mazapan coverage` lists the installed apps (their
  .desktop files) and whether the theme reaches them. Plugins declare what
  they theme (`[coverage]`: apps and toolkits); an app's toolkit comes
  from the libraries its binary links, what its script imports, or its
  package's dependencies (GTK 4/3, Qt 6/5, Electron, Chromium, Firefox, a
  terminal, a web app, a Flatpak, its own UI). The picker shows it:
  "reaches 15 of 17 installed apps · not yet: kitty…". Audited against a
  real machine (61 apps; 9 findings, all fixed).
- *Done* — Qt 6 apps, KDE's included (plugin `theme-qt`): qt6ct with the
  theme's palette, font and the Fusion style, and KDE's color scheme in
  kdeglobals. Both files are shared with their apps (`merge = "ini"`: the
  core manages only its keys), since qt6ct and KDE apps write there too:
  a person's own values for those keys are a conflict the first time
  (--adopt backs them up), a rollback puts back only Mazapán's keys and
  never deletes the file, symlinked dotfiles stay links. On a real machine
  it took coverage from 19 to 27 of 61 apps. Audited (6 findings, all
  fixed).

- *Done* — browsers, in every profile (targets with `each`):
  - Firefox and its forks (Zen, LibreWolf, Floorp, Waterfox; plugin
    `theme-firefox`): the frame (tabs, toolbar, address bar, menus,
    sidebar, Zen's own variables) and the browser's own pages, from a
    stylesheet imported into userChrome.css/userContent.css, so a
    person's own CSS and user.js stay theirs (`lines` and `prefs`).
  - Chromium, Chrome, Brave, Edge (plugin `theme-chromium`): their GTK
    mode, set in each profile's Preferences (`json`): the frame, tabs,
    toolbar and font follow the theme (through theme-gtk). Left alone
    while the browser runs (it would write its copy back): "busy", applied
    next time. Their accent stays theirs: only a system policy (/etc)
    sets it, and one that sets a theme color (Omarchy's) wins over GTK
    mode.
  - Audited (12 findings, all fixed): a JSON file that doesn't parse is
    never rewritten; keys with dots; an @import only counts at the head of
    the file; taking Mazapán's line out of your own file is respected, and
    its lines leave with the plugin; profiles linked twice count once;
    rollbacks don't bring back deleted profiles; permissions kept (0600
    Preferences); contrast of Firefox's buttons and links; high contrast
    mode left alone; coverage claims the browsers, not every app built on
    them.

- *Done* — developer tools:
  - Neovim (plugin `theme-neovim`): a full colorscheme from the theme
    (syntax, Treesitter, LSP, diagnostics, diff, git, Telescope, which-key,
    its terminal's 16 colors), see-through when the terminals are; in
    Neovim's data folder, never in your ~/.config/nvim; used on its own
    when your config sets no colorscheme (else set yours to "Mazapán");
    running Neovims recolor on apply.
  - VS Code, Code - OSS, VSCodium, and their Flatpaks (plugin
    `theme-vscode`): the theme as color customizations in settings.json
    scoped to VS Code's default theme (whatever it's called in that
    version: "Dark 2026", "Dark Modern", "Default Dark Modern"), so a theme
    you pick yourself is left alone; VS Code follows the system's dark or
    light. Open windows follow a new theme at once (switching between dark
    and light takes a restart). A settings.json with comments is left
    alone: a new plan state, `unreadable`; one that's formatted stays so.
  - btop (plugin `theme-btop`): a btop theme from the theme's colors, over
    the terminal's background; btop.conf shared with btop.
  - Audited (10 findings, all fixed): a rollback never rewrites a
    settings.json it can't read; reload commands time out (a Neovim
    suspended with ctrl-z hung apply); busy and unreadable files aren't
    counted as an update; JSON keeps its layout; Mazapán's JSON keys leave
    with the plugin; readable fuzzy matches on VS Code's selected rows;
    colors with alpha trimmed for Neovim and btop.

**Follow-ups, by reach (for everyone, not one machine).** Chromium's
accent through its policy (needs root: a way for plugins to write system
files); Electron apps one by one (Obsidian, Discord…); Qt 5 (qt5ct). Also: GTK apps previewed live
(they follow on ↵); light/dark at sunset; restart the browsers' theme
without restarting them.

## 5. Hardware

**Omarchy today.** 26 scripts for specific models (ASUS ROG, Dell XPS,
Framework 16, Surface…), matched on the DMI product name.

**Much better.** *Done* — hardware plugins ([plugin-api](plugin-api.md#hardware-plugins)):
- Declarative match rules in the manifest (`[hardware]`: DMI maker, model
  and board; PCI and USB ids; a GPU's name from the PCI database; input
  devices; loaded modules; how many GPUs), read from /sys without root.
  `mazapan hardware` shows the machine and the fixes for it; `doctor` offers
  the ones that are off. Never applied on their own, never on a machine they
  aren't for (a config.toml shared with another computer).
- System files, as root with sudo, only as `mazapan*` drop-ins in folders
  made for them (modprobe.d, udev, mkinitcpio.conf.d, sysctl.d…), and
  packages: only on `mazapan apply --system`, previewed with `--dry-run
  --diff`, undone with `mazapan undo` (files back, packages out). A plain
  apply, the theme picker's or an agent's, never asks for a password.
- First plugins: the several-GPUs software cursor (this machine's fix: an
  RTX 4090 rendering for screens on the Radeon iGPU through a dock), Apple
  and Mac-mode keyboards' function keys (hid_apple fnmode, applied at once),
  NVIDIA's open driver for Turing and newer (packages, early KMS in the
  initramfs, Hyprland's variables), Intel video decoding, Synaptics
  InterTouch, the Yoga Pro 7's bass speakers.

- Audited (15 findings, fixed): user-writable state (owned.json, snapshots)
  reached root writes, deletes and pacman arguments (one guard where sudo
  runs now: only mazapan* drop-ins, package names that can't be options);
  what's a system file was decided by the path under $HOME; every plugin's
  packages were installed (hardware plugins' only now); undo could stick on
  a package something needed; removing a system file skipped its reload
  (mkinitcpio); packages came after the files that need them; agents could
  turn hardware plugins on or undo root changes; nothing was confirmed
  before sudo, and text settings reached root files; unplugging a keyboard
  took its fix away; Intel and Synaptics rules misfired; NVIDIA's variables
  were global on hybrid laptops; a literal [ never matched.

**Follow-ups.** More of Omarchy's fixes (Surface, Framework, ASUS ROG
through asusctl, T2 Macs); AUR packages; sharing fixes in a catalog; the
Chromium accent through its policy (the system files are there now).

## 6. An agent-native system

**Omarchy today.** `omarchy-agent-crash` opens a coding agent in a terminal
on a crash.

**Much better.** *Done* — [docs/agent-api.md](agent-api.md):
- State an agent can read: `mazapan status --json` (theme, plugins and where
  they come from, generated files that differ, updates, what can be undone;
  versioned), `doctor --json`.
- Changes previewed exactly: `mazapan apply --dry-run --diff`, with settings,
  enabling and disabling plugins as flags (`--set bar-clock.font_size=11`),
  checked against each plugin's settings.
- Every apply undoable: `mazapan undo` puts back files, ownership and
  config.toml, never over what changed since.
- `mazapan mcp`: the same as an MCP server (status, doctor, themes, plugins,
  coverage, history, preview_change, apply_change, undo), so any agent uses
  Mazapán the way a person does. Installing plugins and updating the system
  stay the person's.
- `mazapan report` and the `agent` plugin: "Ask an agent about this desktop"
  in the palette opens one with the state, failing checks, recent crashes
  and the errors Hyprland and the shell logged.

- Audited (14 findings, fixed): an agent could put code in a text setting
  that a template wrote into Lua (text settings are quoted for the code
  they go in now, and agents only set numbers and switches); one malformed
  request ended the MCP server; undo could replace a symlinked config.toml,
  leave ownership wrong when it stopped half way, or undo the person's
  change instead of the agent's (undo ids); snapshots were public, piled up
  when nothing changed, sorted by local time and weren't crash-safe; applies
  could overlap (a lock now); --set text and settings of disabled plugins
  went unchecked; a theme id could be a path; the diff missed shared files
  and final newlines and could take gigabytes; the report could exceed an
  argument's size.

**Follow-ups.** Offer "ask an agent" on its own when a check fails after an
update or the shell crashes; edit config.toml in place (today it's rewritten
whole, as before, so comments in it don't survive a change).

## 7. Plugins

**Omarchy today.** Shell plugins from git, with a catalog and a manifest
schema.

**Already better.** Ours cover everything (Hyprland, themes, app configs,
bar widgets), with typed settings, localization, and clean removal that
never touches files you edited.

**Much better.** *Done* — `mazapan plugins list|show|enable|disable|add|
update|remove|sync`:
- Dependencies: `requires = ["shell-bar", "hypr-base >= 0.1"]`. apply
  refuses while one is missing, disabled or too old; `disable` says what
  would have to go with it (all the way down), `enable` what's missing.
- Capabilities worked out from the manifest, not declared: full access
  (code, and configs that can run commands), the commands it runs written
  out as they'll run (defines inlined, settings' defaults), the files, the
  packages. Approved at `add`; an `update` that needs more asks again.
- Plugins from git, pinned in `~/.config/mazapan/plugins.lock` (source,
  ref, commit, approvals): `sync` reproduces them on another machine.
  apply refuses one that moved, was edited (even an ignored file), or
  needs more than approved.
- Audited (17 findings, all fixed): commands were approved as raw
  templates, so a changed define or default ran unasked; a plugin could
  write into Mazapán's own folders (plugins.lock, a decoy plugin); symlinks
  and submodules in a repo; index flags and ignored files hid edits; git
  ran with the user's hooks and config; the lock's values reached git
  arguments; an update was checked out before it was approved; a broken
  manifest broke every command; a checkout without its lock entry passed
  as your own; ref switches that were lost; updates in the wrong order.

**The ecosystem.** *Done* (2026-09-29), after studying Omarchy's: its
plugins are QML only (bar widgets, panels, overlays), `manifest.json`, git
add/update, hot reload, `clone` of a built-in, `validate`, a settings
schema for widgets; its "catalog" lists only what's installed; no lock, no
dependencies, no permissions. Ours:
- Settings with a schema: the comment above a setting is its description;
  `{ default, choices, min, max, step, kind, label }` for more; enforced by
  apply, shown as controls, translated (`setting.KEY`).
- Catalogs (`catalog/index.toml`, and `catalogs = […]` in config.toml, URLs
  cached a day): `plugins catalog|search|preview`, `add ID`; entries
  translated (`translations.es.*`); `update` follows the catalog's ref.
- The Plugins panel (`plugin-manager`, SUPER + SHIFT + P), like VS Code's
  extensions: search, tabs, each plugin's page (README in your language,
  what it can do with the risky marked and translated, settings as
  controls); on/off and settings through `mazapan apply` (undo takes them
  back), install only of the commit it showed; the panel survives the shell
  reloading under it.
- Every plugin's name and description translated (`plugin.name`,
  `plugin.description`, `README.es.md`); all built-ins in Spanish, tested.
- Tools for authors: `plugins new --kind bar|panel|window|theme|tools`
  (working, described, in en and es), `dev` (applied on every save, template
  and QML errors in the terminal, a repo elsewhere linked in), `check`
  (every theme × every language, what's missing to describe or translate
  it), `fork` and `diff` (a built-in to change, and what you changed).

**Follow-ups.** A "customize" button in the panel (fork from there);
ratings or download counts need a server; `mazapan update` offering plugin
updates too; signed tags; screenshots in catalog entries.

## Notifications

**Omarchy today.** Its own server in its shell: themed colors, updates in
place, Do Not Disturb that survives restarts. But the actions it
advertises aren't drawn (only a click's default), links don't open,
banners show on every monitor at once, the app's timeout is ignored below
5 s, urgent ones from other apps are silenced by Do Not Disturb, the stack
has no limit, fonts are hardcoded, and there's no center: "history" is the
last 10 replayed as banners. A notification that says it's Omarchy's gets
through Do Not Disturb and runs a command when clicked.

**Ours.** *Done* (2026-09-29), the `notifications` plugin, after macOS:
- Banners on the focused screen, three at most then "+N", in from the
  right; the pointer stops them all and shows their actions (and Reply);
  the app's timeout honored, urgent ones stay with a red edge; progress
  shown; a click runs the app's default action or brings its window.
- A quiet center (SUPER + N, the bell): only notifications, stacked by
  app, relative times, actions and inline replies, clear per app or all;
  per app, banners or center only, and through Do Not Disturb or not.
  Nothing the bar already shows.
- Do Not Disturb by hand, on a schedule, and in full screen; urgent ones
  through (a setting); what was missed told in one banner at the end.
- Text only (no links opened, no images fetched), no commands from
  notifications (per-app rules go by the name an app gives, as anywhere);
  kept across restarts and shell reloads, never shown twice; private to
  its owner. Held back while a screenshot or a recording is made. Capture
  and the theme picker notify through it.

**Follow-ups.** Open a screenshot from its notification; the CLI's own
events (an update done, a check failing) as notifications; per-app sounds.

## 8. Capture

**Omarchy today.** Screenshot, region, screen recording (with webcam), OCR
text and QR as separate scripts.

**Much better.** *Done* — plugin `capture` (`Print`, or the palette):
- One overlay on every screen, over a frozen picture of it (grim, at each
  screen's own resolution): drag a region, or click a window (or empty
  screen) to take it all; the region's size in pixels as you drag.
- Then, next to it: copy, save, copy its text (OCR, in the system's
  language; read at twice the size, which Tesseract reads much better),
  annotate (pen, arrow, box, marker, in the theme's colors, undo), or
  record it (wf-recorder; a red dot and the time in the bar, click it or
  press `Print` again to stop). ↵ copies and saves; c s t a r.
- Each action shows the command it runs (`tesseract shot.png - -l eng |
  wl-copy`), like the palette.
- What's copied or saved is the region as it was, with its drawings, at
  the screen's own pixels (snapped to them: no blur at fractional
  scales); the overlay itself never is, nor a notice (none while
  recording; earlier ones are dismissed before the screens freeze).
- Audited (10 findings, all fixed): a recording is tracked by its pid, so
  one that died never blocks the key; a failed recording (or a missing
  grim, tesseract, wf-recorder) says so instead of "saved"; clicks on the
  toolbar never reset the selection; the window on top is the one picked
  (fullscreen, floating, special workspaces); frozen screens and shots
  are deleted as soon as they're used; OCR uses the installed languages;
  the action keys follow the language (g for guardar).

**Follow-ups.** A region across two screens; blur/pixelate as a tool;
share (upload/send); QR codes; recording with the webcam; a window-follow
recording.

---

# Part two: what's still missing, and what would set it apart

## 9. Wallpapers, and a theme from any picture

**Omarchy today.** Each theme ships three or four pictures; yours go in a
folder per theme; a key cycles them, a picker shows thumbnails, the
change is animated. The picture and the colors know nothing of each
other: a photo of your own doesn't change the rest.

**Here today.** The wallpaper is drawn from the theme's colors (or is the
theme's picture). Your own pictures can't be used: behind Omarchy.

**Much better.**
- A wallpaper picker like the theme picker: your pictures
  (`~/Pictures/Wallpapers`), the theme's, the drawn ones; previewed live;
  one per monitor; fill, fit or center.
- A folder in turn, every so often; dynamic wallpapers as on macOS (a day
  and a night picture, by the time of day).
- "Tint with the theme": any picture subtly recolored to the palette, so
  it belongs.
- **A theme from any picture**: pick a photo, and its palette becomes a
  whole theme, contrast-checked (the checker is there), applied to
  everything the themes reach (terminal, GTK, Qt, browsers, VS Code,
  Neovim, the shell). Every picture, a coherent and readable desktop.

*Done* (2026-09-29):
- `mazapan themes from-image PICTURE [--apply]`: the picture's colors
  (k-means in OKLab, the same every time) become every token: surfaces
  barely tinted in its main hue, text neutral, the accent the color that
  stands out (a color's shades count together, a hue unlike the backdrop
  wins: a jellyfish's orange, not its sea), the status and terminal
  colors in their usual hues; every contrast the themes promise, held
  (tested on light, dark, grey and one-color pictures). Font, shape and
  motion stay the theme's in use; the picture goes next to theme.toml,
  as its wallpaper. The same picture again is the same theme, made
  over; `mazapan themes remove ID` takes one away. Pictures are read by
  ffmpeg or ImageMagick.
- The wallpaper plugin: your pictures (`~/Pictures/Wallpapers`), per
  screen or all, fill / fit / center / tile, tinted with the theme (off,
  subtle, strong), a new one every 15 minutes, hour or day
  (`SUPER + ALT + W` next), and day/night pairs (`name-day.jpg`,
  `name-night.jpg`); faded in; kept in its own state file, so a change
  reloads nothing.
- The picker (`SUPER + SHIFT + W`): thumbnails, the one under the pointer
  (or the keys) live on the desktop behind it, a click keeps it; "Theme
  from this picture" (`t`) makes and applies the theme and says so in a
  notification.
- Audited (17 findings, all fixed; every contrast held over 6,012
  generated palettes): the crossfade could stay on a previewed picture;
  the whole picture is read (not its middle), transparency counts as grey
  either way, one tool failing falls to the other, pictures Qt can't show
  are kept as PNG; the theme is swapped in only once the new one loads;
  a failed theme leaves the wallpaper as it was; rotation counts from a
  kept time (reloads and logins don't restart it); a screen can have the
  theme's own while the others have a picture; names with # or ? load.

## 10. The essentials still missing

What Omarchy has and this doesn't yet, in order of need:
- **A polkit agent**: the password prompt apps need to ask for rights
  (mount a disk, change the time). Without one those fail silently.
- **An on-screen display** for volume and brightness keys.
- **Clipboard history**: find and paste what was copied before.
- **Emoji and color pickers**.
- **Idle**: lock and suspend after a while; **night light** (warmer at
  night).
- **Brightness and battery** in the bar, for laptops.
- **Web apps**: a site as an app of its own (WhatsApp, Gmail).

*Done* (2026-09-29), each one a plugin, in English and Spanish:
- `polkit`: the password prompt, in the theme, over a dimmed screen: what
  is asked for and by what, whose password (several: pick one), a shake
  on a wrong one, the fingerprint reader's messages; Esc, Cancel or
  SUPER + Q refuse it; `mazapan doctor` checks it's the session's agent.
- `osd`: the volume, microphone, brightness and media keys (on the lock
  screen too, repeating when held). What they did, for a moment, on the
  focused screen, never in the way (clicks go through). Volume and the
  microphone as PipeWire has them, whatever changed them; the track
  once the player says what it did; the backlight only (never a
  keyboard's LED).
- `bar-battery`: only where there's a battery. Its level, time left and
  health, the power profile, the screen's brightness (on the same curve
  as the keys); a warning when low and an urgent one when very low, once
  each, however many screens and reloads.
- `clipboard` (`SUPER + CTRL + V`): text and pictures, searchable, pinned
  ones kept; ↵ pastes where you were (Ctrl+Shift+V in terminals). One
  copy is one entry; what's marked secret is never kept; files only you
  can read, and only clip-store's own names are ever read or removed.
- `emoji` (`SUPER + period`): every emoji (Unicode 18), found by its
  name in your language or English, accents or not, the best match
  first; the last used first; typed where you were.
- `color-picker` (`SUPER + SHIFT + C`): copied as CSS writes it (hex,
  rgb, hsl), in a notification with a swatch of it.
- `idle`: the screens dim for 15 s first (a move and they're back), then
  lock, turn off, and it suspends (sooner on battery); locked before any
  sleep; videos and calls keep it awake, and so does "Keep awake" (a cup
  in the bar). hypridle, started in the desktop's session.
- `night-light`: warmer at night, fading in and out over half an hour
  rather than at once; by hours or from sunset to sunrise worked out
  here (no location service); on or off by hand until the schedule
  agrees.
- `webapps`: added from a panel (name and address, the site's icon, or
  one of a few suggestions with a click; none unasked), opened in the
  Chromium-based browser there is; opened again, its window comes
  forward.

Also: the bar no longer overlaps on narrow screens (the center moves
aside, the window's title shortens first); a notification's missing
icon shows the bell, not a checkerboard; the kit's text field lets a
panel's keys go first (arrows, Delete, Esc).

## 11. Modes

**Omarchy today.** Nothing like it: Do Not Disturb, by hand.

**Much better.** A mode changes the whole desktop at once, as macOS's
Focus does for notifications: the theme and wallpaper, Do Not Disturb and
which apps may still notify, the power profile, what the bar shows.
Work (Slack yes, social no), Presentation (no notifications, no ticker,
larger text), Night (dark theme, warm light), Game (performance, quiet).
On by hand, on a schedule, or when a monitor is plugged in. Built on what
9 and 10 add (wallpapers, night light, power) and on notifications.

*Done* (2026-09-29), plugin `modes` (`SUPER + ALT + M`, the palette, the
bar). A mode can be quiet and still let some apps through (any part of
their name), switch the theme (and the wallpaper, when it's the
theme's), hold night light on or off, pick the power profile (only the
ones the computer has), keep it awake (it still locks before any
sleep), and take widgets out of the bar. On by hand, on a schedule
(days and hours, past midnight too), or while a second screen is
connected; one at a time, and off, everything goes back to how it was.
An automatic one turned off by hand waits for its next time. Four to
start with (Work, Presentation, Night, Game), edited in a panel; the
bar shows the one that's on by its glyph, as macOS does. The plugins it
drives each gained a small IPC for it: `notifications setMode`,
`nightlight hold`, `mazapan hide`/`widgets`. Left for later: larger text
for presentations.

## 12. The desktop's history, visible

**Elsewhere.** Whole-system rollback is known: NixOS generations (every
config change one, listed in the boot menu), openSUSE's Snapper on btrfs
(before and after every install, bootable, YaST lists the files),
Fedora Silverblue / Bazzite / Vanilla OS (the previous image, booted),
Linux Mint's Timeshift. Omarchy too: a Snapper snapshot before each
update, bootable from Limine. Here, today: packages and files rolled
back (1), every apply undoable (6), but a system that no longer boots
can't be saved. macOS's Time Machine is the visual timeline, for files.

**Here today.** The data is there: every apply is a snapshot
(`mazapan undo --list`), every update in `mazapan history`. Seen only in the
terminal.

**Much better.** Both, in one place:
- **The desktop's timeline**: a panel of what changed, in words ("Theme:
  Gruvbox → Paper", "Plugin Markets turned on", "Update: 34 packages",
  "Max volume 1.25"), each with a preview and its own undo: one change
  back, at once, no reboot (NixOS and Snapper take the whole system back
  to a point).
- **System snapshots** where the disk is btrfs: Snapper before every
  update, bootable from the boot menu (as Omarchy and openSUSE), shown in
  the same timeline. Closes 1's pending safety net.

Only what goes through Mazapán is in the desktop's timeline: a file it
doesn't manage, edited by hand or by its app, isn't. The system snapshots
cover the rest.

*Done* (2026-09-29):
- `mazapan timeline [--json]`: every apply in words, from config.toml
  before and after (each snapshot now keeps both; older ones are read
  from their neighbours): theme, accent, language, a plugin on or off, a
  setting from one value to another (the default said), or whose files
  were rewritten; every update; every system snapshot.
- `mazapan timeline undo ID`: one change undone on its own, even an older
  one: only what's still as it left it, through a new apply (on the
  timeline too). Files only: the last apply's. Updates: `mazapan rollback`.
- Plugin `history` (`SUPER + ALT + H`): the timeline by day, a theme's
  palettes before and after, each entry's undo (run detached: the apply
  reloads the shell), an update's rollback in a terminal.
- Hardware plugins `hw-snapshots` (root on btrfs: snapper, and snap-pac
  before and after every package change) and `hw-snapshots-grub` (GRUB:
  the snapshots in the boot menu via grub-btrfs). A snapshot started from
  the menu brings up the whole desktop on an overlay in memory
  (systemd.volatile=overlay; the initramfs gets what it needs); checked
  by booting one in the VM. Hardware rules gained `filesystem` and
  `bootloader`. Limine isn't covered yet (limine-snapper-sync is AUR
  only).

## 13. The same desktop anywhere

`config.toml` and `plugins.lock` already make another machine the same
(`mazapan plugins sync && mazapan apply`). One step for it: synced through
git (or a service), with what's per-machine (monitors, hardware) kept
apart.

## 14. Workspace sessions (dropped)

A workspace saved as a whole ("trading": these apps, on these
workspaces, arranged so) and brought back with one key. Dropped on
2026-09-30: Hyprland's workspaces already do what's needed.

## 15. The CLI in your language (dropped)

The plugins are localized; Mazapán's own messages (apply, update, doctor)
are English only. Dropped on 2026-09-30: the CLI stays in English, as
most command-line tools do; everything with a window is localized.

## Part three: what Omarchy has that this doesn't yet

Omarchy today, in general: some 440 bash scripts behind one menu (a
declarative tree, good), but almost everything that changes the system
opens a terminal with gum prompts, keyboard, input, monitors and
keybindings are hand-edited Lua, package operations run `--noconfirm`,
nothing is localized, and there's no undo but the bootloader's snapshots.
What follows keeps its good ideas and fixes those.

## 16. Installation and first boot

**Omarchy today.** An ISO, the only supported way: a configurator (gum
forms on a TTY: keyboard, user, one password, hostname, timezone) writes
archinstall's JSON, and archinstall installs from an offline mirror
inside the ISO, so nothing is downloaded and it's fast. The whole disk,
only which one is asked: btrfs, LUKS optional, Limine with UKIs. A drive
labeled `cidata` makes it unattended. Opinionated: its whole app
selection is installed, and you remove what you don't want.

**Much better.** Its base (it works), a better experience on top:
- The same foundations: an ISO with an offline mirror, archinstall
  behind it driven by a JSON, the whole disk with only which one asked,
  btrfs (with `/boot` on it, so the snapshots of 12 boot from day one),
  encryption optional, unattended with `cidata`.
- A graphical installer in the desktop's own design (Quickshell), few
  screens, everything the machine can tell detected: language (and the
  keyboard from it), Wi-Fi only if there's no cable, the disk, your
  account (one screen), and profiles.
- Profiles, as Windows 11 asks "how will you use this device?": cards
  (Basic, Development, Gaming, Creative, Office, Streaming, Trading…),
  several at once, each showing a few of its apps; "See details" opens
  the full list with checkboxes, for whoever wants it. The profiles'
  apps are in the offline mirror too; the few too big for it arrive in
  the background after the first boot, with progress in the bar.
- Apps arrive configured and in the theme: each comes with its Mazapán
  plugin (a theme, its integration) where there's one.
- Built in steps, each usable on its own: the catalog and the Apps menu
  (17) first, on any Arch; then a first-boot welcome (language, keyboard,
  theme, profiles, Wi-Fi) for those who installed Arch themselves; then
  the ISO, reusing both. Tested by booting the ISO in the VM.

*Done so far* (2026-09-30): the catalog and the Apps menu (17), and the
welcome (plugin `welcome`): on the first login after the machine's first
`mazapan apply`, a screen each for the language (the whole desktop
switches at once), the keyboard (the common ones in your language, any
other by search, a field to try it), the time zone (by city, or from the
connection when asked), Wi-Fi when there's none, the look, and the
profiles (several at once, installed in the background); the last screen
shows the keys as they are on that machine. Where it was is kept across
the reloads it causes; Esc closes it until the next login.

*The ISO* (2026-09-30): `iso/build` (in the dev VM: `vm/vm iso`) makes it
from archiso's own profile, with Mazapán as a package (`pkg/PKGBUILD`) and
an offline repository inside (everything an install puts on the disk,
~550 packages): an install downloads nothing and takes a minute or two.
It starts into the live desktop, in the theme, and the installer (plugin
`installer`, only on the live system: `[hardware] live = true`) opens by
itself: language (the live desktop switches at once), keyboard (tried
there), where you are, Wi-Fi only without a connection, the disk (all of
it, only which one, with what's on it now said), the account (user and
computer names from yours), profiles (several at once, each app to see
for whoever wants to), a review, and a progress bar. `mazapan install run`
turns it into archinstall's configuration: GPT, btrfs (/, /home, logs,
pacman's cache), `/boot` on btrfs (the snapshots boot with their kernel;
encrypted, `/boot` is the EFI partition), GRUB, NetworkManager, PipeWire,
zram, the locale from the language and the time zone (es + Mexico City:
es_MX), the console keymap from the layout; the account's desktop is
written right there, so the first start goes straight in. The login
screen (plugin `login`: greetd, a Hyprland of its own, a Quickshell
greeter in the theme; opt-in, `[hardware] any = true`) instead of the
text login; encrypted, the disk's password is the login. On the first
login the welcome picks up at the look, and the apps chosen install as
soon as there's a connection. For anyone, anywhere: the languages
offered are the translations there are (a new one shows up by itself, in
its own name), the time zone comes from the connection, the keyboards
suggested first are the country's and the language's (Japan's in Tokyo,
Latin America's for Spanish in Mexico), names in any script (a user name
to start from when it isn't Latin, Chinese/Japanese/Korean fonts when
they're needed), the clock as the language writes it, the mirrors the
country's (reflector). Tested in `vm/vm try` (UEFI, blank disk): Spanish
in Mexico, English in Tokyo with a Japanese name, encrypted (the disk's
password typed at boot, straight in) and not. Audited: the live system
never locks or sleeps, installs are started by a click (never Enter), the
stick it runs from is never offered, a failed install's leftovers are
taken down, SSH keys (only when the live system was reached with them)
are shown and keys-only. Snapshots from the install on (2026-09-30):
hw-snapshots and, with `/boot` on btrfs, hw-snapshots-grub turned on
while installing, with a first snapshot of the system as installed,
already in the boot menu; every package change adds its pair (the
History panel lists them). Mazapán speaks Portuguese, French and German
too (every plugin, the app catalog, the emoji names, the password
dialog), and the installer offers the world's main languages, in
English where Mazapán isn't translated yet. Next: unattended installs
with `cidata`, and trying it on real hardware.

## 17. Apps: install and remove

**Omarchy today.** Menu branches (Install / Remove) whose rows hide when
the app is already there or not (good), each opening a terminal that
runs `pacman -S --noconfirm`; fzf over raw package names for anything
else; AUR through yay; a hardcoded list of preinstalls and its removal;
Remove offers every explicit package, core ones included.

**Much better.** An Apps menu that already knows how to install what it
offers, from the same catalog as the installer's profiles:
- The catalog is data, not code: apps (what they are, their packages
  from the official repositories or their Flatpak, the mazapan plugins
  that go with them) and profiles (sets of apps); in English and
  Spanish; others can add catalogs, as with plugins.
- Profiles on top ("Install the Gaming profile"), then every app by
  category and a search; what's installed marked, with Open and Remove.
- Simple by default: one button. Before it runs, what it will do in one
  line (5 apps, 1.2 GB), the detail (every package) one click away.
- The password through the polkit agent, progress in the panel, no
  terminal; no AUR (Flatpak for what the repositories don't have).
- Every install and removal on the timeline (12), with its undo; only
  what the catalog installed is offered for removal, never the system.

## 18. Settings with a face

**Omarchy today.** Font (monospace, sed into each terminal's config) and
one text-size knob across shell, GTK and terminals (good); timezone from
a picker; touchpad and touchscreen toggles; everything else (keyboard
layout, repeat, natural scroll, monitors, keybindings) is opening a Lua
file in the editor.

**Much better.**
- A Settings panel with a page per thing, each one a plugin's settings
  shown with their kinds (the Plugins panel already renders them):
  keyboard (layouts and variants, a switch key, repeat, a field to try
  it), mouse and touchpad (speed, natural scroll, tap), text size and
  fonts (UI and monospace, from what's installed, previewed), timezone
  and 24-hour clock, default apps, language.
- Keybindings: every binding (they're all palette actions already) in
  one list, changed by pressing the new keys, conflicts said.
- Changed live with a preview; risky ones (a layout you can't type your
  password in) with a revert timer like the monitors'.
- All on the timeline, each undoable.

## 19. Security

**Omarchy today.** Fingerprint set up only after enrolling and verifying
works (good), with a closed-lid gate; FIDO2 keys for sudo and polkit;
passwordless sudo for N minutes with an expiry timer (good); hibernation
(CLI, Limine only); change the disk password; all through sed on PAM
files, in a terminal, one finger only.

**Much better: what a macOS user expects, built the way it's already
proven.** Touch ID, FileVault, a lock that's there before the lid opens,
the orange and green dots, a firewall switch, no work lost when the
battery dies; all from a Security page in Settings, no terminal, each
step undoable. This is where a mistake locks people out of their own
machine, so nothing here is invented: each part copies what Arch-based
distros, GNOME, KDE and the Arch Wiki have done for years, and avoids
what's broken for others (research of 2026-09-30, sources below).

Order: 1, then 2, 3, 4, 5.

1. **The disk, like FileVault.**
   - Encrypted by default in the installer (a switch turns it off), the
     person's password as the disk's (as Omarchy). LUKS2 (argon2id) on the
     root only; the ESP unencrypted at /boot, GRUB never opening LUKS
     (as archinstall, Fedora, Ubuntu). Not an encrypted /boot, as the
     Calamares distros do: GRUB can't open argon2id, and in 2026 Garuda
     and CachyOS installs don't boot because of it.
   - The initramfs with systemd's hooks (sd-encrypt, sd-vconsole) and the
     installer's keymap in it: a password typed in another layout is
     the known "my password stopped working".
   - A recovery key always (`systemd-cryptenroll --recovery-key`), shown
     at the end of the install as Ubuntu does: as text and a QR code,
     and not finished until the person says it's kept.
   - One password: typed once at boot, then straight into the desktop
     (greetd logs in by itself that boot), the keyring opened by the same
     password through pam_systemd_loadkey (systemd 255+; what GDM has
     long done). Not Omarchy's keyring without a password: its secrets
     sit in plain text, and it has broken for them more than once.
     greetd's autologin may skip PAM's auth step: checked in the VM
     first.
   - Changing the password: one place for the disk, the account and the
     keyring. The disk first, then checked that the new one opens it,
     then the account; the keyring follows through PAM. Stopped halfway,
     running it again finishes it.
   - TPM2 unlock later, opt-in: only with Secure Boot on the machine's
     own keys and a PIN (a TPM tied to PCR 7 alone can be fooled, 2025;
     firmware updates make it ask for the key: Ubuntu 23.10, Arch forum
     2024). The recovery key covers it.
2. **Locked before it sleeps, and the dots.**
   - The lock is in place before the machine suspends (logind's delay
     inhibitor, as xss-lock did; hypridle's `inhibit_sleep = 3` with
     `before_sleep_cmd = loginctl lock-session`): nothing on screen when
     the lid opens. A "require the password after" delay applies only to
     idle blanking, never to sleep (GNOME's lock-delay).
   - Microphone, camera and screen sharing shown in the bar while in
     use, and which app: PipeWire's running nodes by media.class (as
     Waybar's privacy module), open /dev/video* handles for browsers that
     skip PipeWire, Hyprland's screencast event.
   - Per-app permissions: the portal's permission store and Flatpak
     overrides (Flatseal's model), said plainly to hold for Flatpak apps
     only (as Plasma 6.5 does); screen capture asked for any app through
     Hyprland's permissions.
3. **Fingerprint, like Touch ID.**
   - Password and fingerprint at the same time, each in its own PAM
     stack (GNOME's gdm-fingerprint, KDE's kscreenlocker, Omarchy's lock):
     the lock screen and the polkit agent run both, whichever comes
     first. Never pam-fprint-grosshack.
   - The password always after a reboot (the keyring needs it), as macOS.
   - The system's PAM files are never rewritten (Arch keeps edited ones
     and leaves .pacnew: the 2020 pam_tally2 lockout). Mazapán's own
     services, owned by its package; sudo and polkit-1 get one marked
     line pointing to them. polkit-1 starts from the /usr/lib/pam.d copy
     polkit ships now (Omarchy's short one dropped faillock and more).
   - On only after enrolling and verifying; `max-tries=3 timeout=10`; not
     with the lid closed, not over SSH (polkit 127+ can't tell); no
     faillock in the fingerprint stack (it counts successes as failures).
   - FIDO2 keys the same way: a root-owned file in /etc, `cue`, never
     `nouserok`.
4. **The firewall, one switch.** ufw on, nothing in, everything out (as
   Omarchy and CachyOS); what a plugin needs (LocalSend) opened only to
   the local network (Omarchy's is open to the internet over IPv6, their
   #11560); no SSH port; ufw-docker only when Docker is there; ufw's
   own rules keep printers and mDNS discovery working.
5. **Nothing lost when the battery dies.** A swap file the size of RAM in
   its own top-level @swap subvolume (inside root, a snapshot rollback
   takes it: Omarchy's), below zram; the lid suspends, then hibernates
   after a while or at 5 % (suspend-then-hibernate), and at critical
   battery (UPower); resume found by systemd, with resume= on GRUB's
   line as well.

Not now: AppArmor (no Arch-based distro turns it on; CachyOS warns it
breaks things); time-boxed passwordless sudo for agents (sudo stays per
terminal, never global or NOPASSWD).

Sources: Arch Wiki (dm-crypt, systemd-cryptenroll, Fprint, Session lock,
Universal 2nd Factor, Suspend and hibernate, Uncomplicated Firewall),
systemd-cryptenroll(1), pam_fprintd(8), sleep.conf.d(5); Lennart
Poettering, "Brave New Trusted Boot World" (2022); oddlama, TPM unlock
bypass (2025); Omarchy's scripts and issues; Calamares' LUKS wiki;
EndeavourOS, Manjaro, Garuda and CachyOS forums and wikis; GNOME
gnome-shell !2840, KDE kscreenlocker !15; Waybar's privacy module;
chaifeng/ufw-docker; Yubico pam-u2f and YSA-2025-01.

## 20. Updates, visible

**Omarchy today.** A bar icon only when Omarchy's own package is behind
(checked every 6 hours); the update is one terminal flow (snapshot,
keyring, `pacman -Syu --noconfirm`, migrations, AUR, mise, orphans, a
reboot prompt); channels (stable, rc, edge); firmware through fwupd in a
terminal.

**Much better.** Built on `mazapan update` (1) and the snapshots (12):
- The bar says there are updates (the whole system's, not only ours),
  checked in the background; a click shows them: what, from which
  version to which, which need a restart, news that needs reading
  first, in a panel.
- Downloaded ahead in the background (on AC and unmetered only), applied
  when you say, with progress in the shell and the checks after.
- Firmware in the same panel: each device, its version and the new one.
- Plugin updates (from their catalogs) alongside, with their diffs.
- After: what needs a restart (the kernel, a service, the shell) said,
  and restarted for you where it can be.

## 21. Boot and login in the theme

**Omarchy today.** One "unlock" look per theme for the boot splash
(Plymouth) and the login screen (SDDM), recolored with ImageMagick and
installed as root with an initramfs rebuild each time; the boot menu
(Limine) keeps its own colors.

**Much better.**
- The boot splash, the login screen and the boot menu (GRUB, Limine,
  systemd-boot) from the theme's tokens, as theme plugins: rendered like
  any other file, applied with `--system`, undoable.
- The login screen a Quickshell greeter (greetd) in the same kit as the
  lock screen: one design from boot to desktop.
- The initramfs rebuilt only when the splash actually changed.

## 22. Sharing

**Omarchy today.** LocalSend send from a menu (clipboard, file, folder)
and its app to receive; Taildrop send and a race-free receive into
Downloads (good); Wi-Fi shared as a QR on screen; a speed test.

**Much better.**
- Share from anywhere: the clipboard history, a capture, the file
  manager's selection, to a device nearby (LocalSend) or on your tailnet
  (Tailscale), with receiving in the notification center (accept, open,
  show in folder).
- Wi-Fi as a QR from the network card in the bar; the speed test there
  too, without a hardcoded token.

## 23. More capture

**Omarchy today.** Text from a region (OCR, English unless an
environment variable says otherwise); a QR decoded from a region and
copied as a secret, never shown (good); a webcam overlay while
recording.

**Much better.**
- In the capture panel (8): text (OCR in your language, and the
  document's), QR (copied as a secret, as Omarchy does), and the webcam
  in a corner while recording, moved and resized with the mouse.
- Text captures kept in the clipboard history; a capture shared (22).

## 24. Extras

**Omarchy today.** A screensaver (text effects in a terminal per
monitor); dictation (voxtype, hold F9); reminders as systemd timers,
with a bar indicator; a crash watcher that offers an AI diagnosis
(good); a Windows VM (docker + RDP); a media converter; tmux/herdr
cheatsheets.

**Much better.**
- Each one a plugin, off until wanted, in the catalog: screensaver in
  the theme (Quickshell, not a terminal), dictation with the model and
  language chosen in its settings, reminders in the notification center,
  the crash watcher feeding the agent API (6).
- The niche ones (Windows VM, converter) as community plugins.

## Also pending

- 34 of the 57 built-in plugins have no README: their page in the
  Plugins panel is empty (`mazapan plugins check` says so).
- `mazapan update` offering plugin updates; Chromium's accent through its
  policy. (The "updates available" widget is item 20's.)

