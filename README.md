# my-arch

A desktop on plain Arch Linux: Hyprland + Quickshell, one theme applied
consistently across the whole OS, and everything extensible through plugins.

Arch owns the critical parts (kernel, packages, updates). This repo only adds
the layer on top.

## Layout

```
core/      CLI `myarch`: plugin loader, merge, generate, apply, rollback, doctor
shell/     Quickshell shell: skeleton, plugin registry, Theme.qml
plugins/   built-in plugins (theme targets, widgets, layouts…), same API as third-party ones
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
`--adopt`. How plugins work: [docs/plugin-api.md](docs/plugin-api.md).

Build and test (inside the VM, which has Go):

```sh
vm/vm run 'cd core && go test ./... && go build -o ../bin/myarch ./cmd/myarch'
vm/vm run 'myarch apply'
```

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
vm/vm run 'vm/guest/motion-probe.py "hl.dsp.layout(\"swapcol r\")"'  # measure an animation
vm/vm shot         # screenshot of the guest desktop -> vm/.state/shot.png
vm/vm provision    # re-run after editing vm/guest/packages.txt
vm/vm stop
vm/vm reset        # fresh first boot, keeps the downloaded image
```

The guest autologins on tty1 and starts Hyprland. Output from its startup goes
to `~/.cache/start-hyprland.log` in the guest.

Knobs (env vars): `MYARCH_MEM` (8G), `MYARCH_CPUS` (8), `MYARCH_DISK` (40G),
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
