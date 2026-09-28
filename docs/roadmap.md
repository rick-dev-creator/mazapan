# Roadmap: what Omarchy has, and how to do it much better

Not a list of features to copy. Omarchy (4.0.4, 441 bash commands) already
ships most of these in some form, including a plugin system and an AI
agent for crashes; each item says what it does today and what "much better"
means here. Ordered by impact.

Suggested start: 1, 2 and 3.

## 1. Updates you can trust

**Omarchy today.** 106 migration scripts run once, in order. `refresh-config`
copies a shipped config over yours (with a backup). Updates take a snapper
snapshot first and grep the log for known failures afterwards.

**Much better.**
- No config migrations: config is declarative and `myarch` knows which
  files it owns, so an update is just a new `apply`.
- A preview before anything changes: packages, generated files, plugins.
- Health checks after the update (Hyprland starts, network, sound, the bar
  loads) and an automatic rollback to the snapshot when one fails.
- Reset one plugin's files instead of a whole config.

## 2. Monitors

**Omarchy today.** About 15 scripts: clamshell, external active, scaling,
mirror, recover the internal monitor, a watcher for removed monitors.

**Much better.** A monitor manager in Quickshell: drag screens into place,
pick mode, scale and rotation, and save profiles matched by EDID that apply
on their own when the screens appear ("desk with 4 screens", "laptop only").
Connector names renumber; EDIDs don't. The original pain: 25 attempts at
one afternoon's monitors.lua.

## 3. One command palette

**Omarchy today.** 14 separate menus (keybindings, clipboard, emoji, share,
capture…); the keybinding list is parsed out of the config.

**Much better.**
- A single palette that searches apps, windows, every plugin's actions,
  settings and files at once.
- Every action shows the command it runs: the "learn to be the hacker"
  mode — you start by feeling like one and end up being one.
- Keybindings listed from the plugins that define them, so the list is
  always right.

## 4. Themes

**Omarchy today.** 34 theme commands and one `theme-set-*` per app; palettes
centered on the 16 ANSI colors. Its own theme preview shows the file
manager unthemed.

**Much better.**
- Semantic tokens that include shape and motion. *Done.*
- Try a theme live on the desktop before choosing it.
- Contrast checked automatically when a theme loads.
- A coverage report: installed apps the theme doesn't reach.

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
