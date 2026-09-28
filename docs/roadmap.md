# Roadmap: what Omarchy has, and how to do it much better

Not a list of features to copy. Omarchy (4.0.4, 441 bash commands) already
ships most of these in some form, including a plugin system and an AI
agent for crashes; each item says what it does today and what "much better"
means here. Ordered by impact.

Suggested start: 1, 2 and 3.

## Progress

| # | Item | State |
|---|------|-------|
| 1 | Updates you can trust | **Done** (2026-09-28); follow-ups listed below |
| 2 | Monitors | **Done** (2026-09-28); follow-ups listed below |
| 3 | One command palette | **Done** (2026-09-28): first version; follow-ups listed below |
| 4 | Themes | **Done** (2026-09-28); follow-ups listed below |
| 5 | Hardware | not started |
| 6 | An agent-native system | not started |
| 7 | Plugins | partly: typed settings, localization, clean removal, checks |
| 8 | Capture | not started |

## 1. Updates you can trust

**Omarchy today.** 106 migration scripts run once, in order. `refresh-config`
copies a shipped config over yours (with a backup). Updates take a snapper
snapshot first and grep the log for known failures afterwards.

**Much better.** *Done* — `myarch update`, `doctor`, `history`, `rollback`:
- No config migrations: config is declarative and `myarch` knows which
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
  files a later `myarch apply` rewrote left alone.
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
rollback can't fix (an unbootable system); AUR packages; an "updates
available" widget in the bar; reset one plugin's files; the CLI's own text
localized like the plugins.

## 2. Monitors

**Omarchy today.** About 15 scripts: clamshell, external active, scaling,
mirror, recover the internal monitor, a watcher for removed monitors.

**Much better.** *Done* — plugin `monitors`:
- Profiles matched by EDID (`~/.config/myarch/monitors.json`), applied by
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
without an HDR screen); theming Hyprland's own notifications (or the
shell's own notification daemon).

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
  the list is always right. Commands that ask or print (`myarch update`,
  `doctor`, `rollback`…) open in a terminal that stays.
- Audited once (9 findings, all fixed). Tested in the dev VM: open with
  the key, search, run an action, launch a terminal app, launch
  an app, run a terminal action, copy a command, close with SUPER + Q.

**Follow-ups.** Files (plocate/fd) and settings (needs `myarch set`);
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
  `myarch apply --accent`). The other accent tokens (text on it, as text,
  a deep tint, the selection) are derived in OKLab with contrast kept.
- *Done* — contrast checked for every theme and accent (`myarch themes`,
  and in the picker): the pairs plugins rely on against WCAG's minimums.
  It caught Gruvbox's red at 4.3:1 on its background. Derived accents keep
  every pair: tested over a grid of 216 accents in every bundled theme.
- Audited once (8 findings, all fixed); a demo recorded in the dev VM.
- Three more themes: Amber (the 80s amber CRT), Gruvbox, and Paper (light).
- *Done* — coverage: `myarch coverage` lists the installed apps (their
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
  (--adopt backs them up), a rollback puts back only myarch's keys and
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
    the file; taking myarch's line out of your own file is respected, and
    its lines leave with the plugin; profiles linked twice count once;
    rollbacks don't bring back deleted profiles; permissions kept (0600
    Preferences); contrast of Firefox's buttons and links; high contrast
    mode left alone; coverage claims the browsers, not every app built on
    them.

**Follow-ups, by reach (for everyone, not one machine).** Chromium's
accent through its policy (needs root: a way for plugins to write system
files); developer tools (a Neovim colorscheme, a VS Code theme, btop);
Electron apps one by one; Qt 5 (qt5ct). Also: GTK apps previewed live
(they follow on ↵); light/dark at sunset; restart the browsers' theme
without restarting them.

## 5. Hardware

**Omarchy today.** 26 scripts for specific models (ASUS ROG, Dell XPS,
Framework 16, Surface…), matched on the DMI product name.

**Much better.** Hardware fixes as plugins with declarative match rules
(DMI, PCI and USB IDs). `myarch doctor` finds the ones that apply and
offers them; anyone can share theirs. First candidate: the hybrid-GPU
software-cursor fix from this machine's Omarchy config.

## 6. An agent-native system

**Omarchy today.** `omarchy-agent-crash` opens a coding agent in a terminal
on a crash.

**Much better.**
- State an agent can read: `myarch status --json`, doctor checks as data.
- Whatever an agent proposes is a config or plugin change that goes
  through `myarch apply`: previewed, reviewable, reversible.

## 7. Plugins

**Omarchy today.** Shell plugins from git, with a catalog and a manifest
schema.

**Already better.** Ours cover everything (Hyprland, themes, app configs,
bar widgets), with typed settings, localization, and clean removal that
never touches files you edited.

**Still missing.** Pinned versions (a lockfile), declared permissions,
dependencies between plugins.

## 8. Capture

**Omarchy today.** Screenshot, region, screen recording (with webcam), OCR
text and QR as separate scripts.

**Much better.** One Quickshell selection overlay: pick a region, then
annotate, copy the text (OCR), copy or share the image, or record it.
