# my-arch

A desktop on plain Arch Linux: Hyprland + Quickshell, one theme applied
consistently across the whole OS, and everything extensible through plugins.

Arch owns the critical parts (kernel, packages, updates). This repo only adds
the layer on top.

## Layout

```
core/      CLI `myarch`: plugin loader, merge, generate, apply, rollback, doctor
plugins/   built-in plugins, same API as third-party ones: theme targets,
           window layouts, the Quickshell bar and its widgets…
themes/    token palettes
docs/      plugin API and design notes
vm/        dev VM
```

## Core

```sh
myarch apply --theme phosphor   # render every enabled plugin and write the files
myarch apply --dry-run          # show new / changed / conflict / orphan, write nothing
myarch apply --adopt            # back up files myarch didn't write and take them over
myarch plugins                  # what each plugin generates
myarch themes
```

It never overwrites a file it didn't write, or one you edited, without
`--adopt`.

Updating the system:

```sh
myarch update --check   # preview: packages (★ = your desktop depends on it),
                        # Arch news since your last update, generated files
myarch update           # preview, confirm, upgrade, re-apply, run the checks;
                        # if one fails, roll back on its own
myarch doctor           # run every plugin's health check now
myarch history          # past updates and how they went
myarch rollback [ID]    # undo an update: previous packages from pacman's
                        # cache, generated files as they were
```

Rolling back needs no reboot: it reinstalls the previous version of exactly
the packages the update changed, from pacman's cache or, when the cache no
longer has them, from the Arch Linux Archive. Generated files go back as
they were; one you edited by hand since is backed up first, never lost.
Checks that need the graphical session are skipped (not failed) when you
update from a TTY or over SSH. How plugins work: [docs/plugin-api.md](docs/plugin-api.md).

Build and test (inside the VM, which has Go):

```sh
vm/vm run 'cd core && go test ./... && go build -o ../bin/myarch ./cmd/myarch'
vm/vm run 'myarch apply'
```

## Monitors

Profiles live in `~/.config/myarch/monitors.json`: which screens, where,
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

Knobs (env vars): `MYARCH_MEM` (8G), `MYARCH_CPUS` (8), `MYARCH_DISK` (40G),
`MYARCH_OUTPUTS` (1; extra outputs stay disconnected with the GTK window),
`MYARCH_RES` (1920x1080), `MYARCH_SSH_PORT` (2222), `MYARCH_DISPLAY`
(`gtk,gl=on,zoom-to-fit=off,grab-on-hover=on`).

The QEMU window follows its own size: resize it and the guest's resolution
follows.

Keyboard and mouse: the mouse moves in and out of the window freely (the
guest has an absolute tablet, not a captured mouse). The keyboard is grabbed
while the pointer is over the window (`grab-on-hover`), so SUPER shortcuts go
to the guest; move the pointer out and they go back to the host.
Ctrl+Alt+G toggles the grab by hand.

Requirements on the host: `qemu-desktop`, `xorriso`, `curl`, `python3`, KVM access.
