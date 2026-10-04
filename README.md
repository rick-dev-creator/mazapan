<p align="center"><img src="assets/mazapan-app.svg" width="128" alt="Mazapán"></p>

# Mazapán

A desktop on plain Arch Linux: Hyprland + Quickshell, one theme applied
consistently across the whole OS, and everything extensible through plugins.

Arch owns the critical parts (kernel, packages, updates). This repo only adds
the layer on top.

> **A personal project, shared as it is.** Mazapán is one person's desktop,
> made public in case it helps someone else. It comes with no warranty of
> any kind (see [LICENSE](LICENSE)): installing it erases the disk you pick,
> so back up what matters first, and read [docs/first-install.md](docs/first-install.md)
> and [docs/security.md](docs/security.md) before trying it on a real machine.

## Layout

```
core/      CLI `mazapan` (C#, Native AOT): plugin loader, merge, generate, apply, rollback, doctor
plugins/   built-in plugins, same API as third-party ones: theme targets,
           window layouts, the Quickshell bar and its widgets…
themes/    token palettes
docs/      plugin API and design notes
iso/       the ISO: archiso's live profile + the installer (built in the dev VM: vm/vm iso)
pkg/       mazapan and mazapan-keyring as pacman packages, the version, releases
vm/        dev VM, and a second one to try the ISO (vm/vm try)
```

## Core

```sh
mazapan apply --theme phosphor   # render every enabled plugin and write the files
mazapan apply --dry-run          # show new / changed / conflict / orphan, write nothing
mazapan apply --adopt            # back up files mazapan didn't write and take them over
mazapan plugins                  # every plugin, where it comes from, on or off
mazapan plugins show palette     # what it needs and what it can do
mazapan plugins add <id|git-url> # a plugin from a catalog or git, after you approve what it can do
mazapan plugins search clock     # the catalogs; SUPER + SHIFT + P is the Plugins panel
mazapan plugins new my-widget --kind bar   # write your own: then plugins dev, plugins check
mazapan plugins sync             # the plugins in plugins.lock (on another machine)
mazapan themes                   # every theme, with its contrast problems
mazapan themes from-image ~/Pictures/sea.jpg --apply   # a whole theme from a picture
mazapan coverage                 # installed apps the theme reaches, and not
mazapan apply --accent '#4fa35f' # your accent in any theme ("theme" = its own)
mazapan apply --set bar-clock.font_size=11 --dry-run --diff   # preview exactly
mazapan undo                     # put back what the last apply changed
mazapan status --json            # the whole state, for agents and scripts
mazapan mcp                      # the same for agents, as an MCP server
mazapan hardware                 # this machine, and the hardware fixes for it
mazapan apply --system           # also system files (/etc) and packages, with sudo
```

Agents (Claude Code, any MCP client) change the desktop the way a person
does: through Mazapán, previewed with the exact diff, undoable. See
[docs/agent-api.md](docs/agent-api.md).

`SUPER + SHIFT + T` opens the theme picker: each theme drawn as a small
desktop from its own colors, previewed live on yours as you move through
them (← →, tab for the accent), terminals already open included; ↵
applies it, esc goes back. Themes also set how see-through terminals are
(with the wallpaper blurred behind) and the wallpaper, drawn from their
own colors.

Notifications come in at the top right, in the theme, with their actions;
`SUPER + N` opens a quiet center stacked by app, and `SUPER + SHIFT + N`
turns Do Not Disturb on (it also turns on by schedule and in full screen).

`SUPER + SHIFT + P` opens the Plugins panel, like an editor's extensions
view: every plugin (built in, yours, installed, and the catalogs'),
searchable, each with its page (README, what it can do, settings as
controls), in your language. Turning plugins on and off and changing
settings go through `mazapan apply`, so `mazapan undo` takes them back;
installing asks first, showing what the plugin will be able to do. Writing your own:
`mazapan plugins new`, `dev`, `check`, `fork` (see
[docs/plugin-api.md](docs/plugin-api.md#writing-a-plugin)).

It never overwrites a file it didn't write, or one you edited, without
`--adopt`.

Updating the system:

```sh
mazapan update --check   # preview: packages (★ = your desktop depends on it),
                        # Arch news since your last update, generated files
mazapan update           # preview, confirm, upgrade, re-apply, run the checks;
                        # if one fails, roll back on its own
mazapan doctor           # run every plugin's health check now
mazapan history          # past updates and how they went
mazapan rollback [ID]    # undo an update: previous packages from pacman's
                        # cache, generated files as they were
mazapan channel [stable|edge]  # where Mazapán's own updates come from
mazapan version
```

An update is a few steps, each with its ✓: getting ready (room, power,
the machine kept awake), the keyrings first when they change, the
packages (Arch's, Mazapán's from its own signed repository, the Flatpak
apps), the configuration written again, the checks; then what needs a
restart, offered. The updates panel shows the same steps, and what's new
in Mazapán from its changelog.

Rolling back needs no reboot: it reinstalls the previous version of exactly
the packages the update changed, from pacman's cache or, when the cache no
longer has them, from the Arch Linux Archive. Generated files go back as
they were; one you edited by hand since is backed up first, never lost.
Checks that need the graphical session are skipped (not failed) when you
update from a TTY or over SSH. How plugins work: [docs/plugin-api.md](docs/plugin-api.md).

The core is C# (.NET 10), built with Native AOT into one native binary
that needs nothing installed next to it. Build and test (needs the .NET 10
SDK and clang), then try it in the VM:

```sh
core/build test                 # run the tests, build bin/mazapan
vm/vm run 'mazapan apply'
```

The desktop runs `mazapan` too (the command palette's actions), so it has
to be on the PATH of the graphical session, not just your shell's. The dev
VM links it into `/usr/local/bin`; on your own machine:

```sh
sudo ln -sf "$PWD/bin/mazapan" /usr/local/bin/mazapan
```

## Monitors

Profiles live in `~/.config/mazapan/monitors.json`: which screens, where,
at what mode, scale and rotation, adaptive sync (VRR), 10-bit color, which
ones mirror another or are off, and which workspaces each one holds.
Screens are identified by their EDID, so connector names that move around
(DP-2 today, DP-3 tomorrow) don't matter. The profile whose screens are
exactly the ones connected is applied at startup and whenever a screen
comes or goes; when none matches, screens a profile had turned off or
mirrored show their own desktop again.

`SUPER + SHIFT + M`, or the screen icon in the bar (with how many screens
there are, when more than one), opens the manager, sized to the screen it
opens on:
live thumbnails of every screen that you drag into place (they snap to
each other, never overlap, and glide into position), and each physical
screen shows its name in large letters while it's open, so you know which
is which. Pick mode, scale, rotation, VRR, 10-bit and mirroring, try the
layout (it goes back on its own after 15 s unless you keep it; what a
screen can't do, like 10-bit on some GPUs, is switched back off and
reported) and save it as a profile.

## Command palette

`SUPER + Space`, or the Arch logo at the start of the bar, searches
everything at once: open windows, installed apps,
and every plugin's actions and keybindings. Each result shows the command
it runs (`$ hyprctl eval 'mazapan_columns.equal()'`) and its key: you start
by clicking and end up knowing the command. ↑↓ to pick, ↵ to run, `ctrl+c`
copies the command, `>` searches only actions and keys. The keybindings
come from the plugins that bind them, so the list is always right.

## Power

The ⏻ button next to the clock: lock, suspend, log out, reboot and shut
down, each with its command; the last three ask for a second click.
`SUPER + L` locks. The lock screen (hyprlock) is in the theme: the time,
the date, and a prompt. All of them are in the palette too.

## Capture

`Print` freezes the screens: drag a region or click a window, then copy,
save (~/Pictures/Screenshots), copy its text (OCR), annotate it, or record
it (~/Videos/Recordings; `Print` again or the red dot in the bar stops it).
Each action shows the command it runs.

## Dev VM

An official Arch cloud image, provisioned by cloud-init, with this repo mounted
live at `/home/arch/my-arch` over 9p: edit on the host, run in the guest.

```sh
vm/vm start        # first run downloads the image (~550 MB) and installs packages
vm/vm wait         # until SSH is up and provisioning finished
vm/vm ssh          # shell in the guest (user arch / password arch)
vm/vm run <cmd>    # run inside the guest's Hyprland session (hyprctl, gsettings…)
vm/vm exec <cmd>   # launch an app through Hyprland
vm/vm key meta_l-equal  # press keys on the guest keyboard (QEMU sendkey names)
vm/vm mouse click X Y   # pointer, in layout pixels (all screens together)
vm/vm run 'hyprctl output create headless TEST-A'  # a screen to plug in and out
vm/vm run 'vm/guest/motion-probe.py "hl.dsp.layout(\"swapcol r\")"'  # measure an animation
vm/vm shot         # screenshot of the guest desktop -> vm/.state/shot.png
vm/vm provision    # re-run after editing vm/guest/packages.txt
vm/vm stop
vm/vm reset        # fresh first boot, keeps the downloaded image
```

The guest autologins on tty1 and starts Hyprland. Output from its startup goes
to `~/.cache/start-hyprland.log` in the guest.

Knobs (env vars): `MAZAPAN_MEM` (8G), `MAZAPAN_CPUS` (8), `MAZAPAN_DISK` (40G),
`MAZAPAN_OUTPUTS` (1; extra outputs stay disconnected with the GTK window),
`MAZAPAN_RES` (1920x1080), `MAZAPAN_SSH_PORT` (2222), `MAZAPAN_DISPLAY`
(`gtk,gl=on,zoom-to-fit=off,grab-on-hover=on`).

The QEMU window follows its own size: resize it and the guest's resolution
follows.

Keyboard and mouse: the mouse moves in and out of the window freely (the
guest has an absolute tablet, not a captured mouse). The keyboard is grabbed
while the pointer is over the window (`grab-on-hover`), so SUPER shortcuts go
to the guest; move the pointer out and they go back to the host.
Ctrl+Alt+G toggles the grab by hand.

Requirements on the host: `qemu-desktop`, `xorriso`, `curl`, `python3`, KVM access.

## License

[MIT](LICENSE). What Mazapán learned from others, and their notices, is in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md); among them Omarchy, whose
hardware fixes the `hw-*` plugins carry. The name "Mazapán" and its icon
aren't covered by the license: a fork is welcome under another name.
