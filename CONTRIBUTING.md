# Contributing

myarch is for anyone, anywhere: any language, keyboard, time zone and
machine. Contributions that make it work better somewhere it doesn't yet
are the most welcome.

Code, comments and commit messages are in English; what people see is in
their language.

## Build and test

```sh
core/build test        # the core (needs the .NET 10 SDK and clang): tests, then bin/myarch
```

Everything that runs myarch runs in a VM, never on the machine you work
on (it rewrites your desktop's files):

```sh
vm/vm start            # the dev VM: Arch + Hyprland, this checkout shared at ~/my-arch
vm/vm run 'myarch apply'
vm/vm shot             # a screenshot of its desktop
```

The ISO is built in the dev VM and tried in a second one, with a blank
disk:

```sh
vm/vm iso              # iso/build in the dev VM: vm/.state/iso/myarch-*.iso
vm/vm try headless     # boot it (UEFI); vm/vm try start to watch it
vm/vm try shot         # the live desktop; vm/vm try mouse/key to go through the installer
```

After an install, `MYARCH_TRY_USER=<the account> vm/vm try ssh` reaches
the installed system (the installer gives it the live system's SSH keys,
which `vm/vm` puts there).

## A language

Every plugin keeps its text in `plugins/<id>/locales/<lang>.toml`
(`en.toml` is required; any other language may leave keys out, and they
show in English). To add a language, add `<lang>.toml` next to `en.toml`
in the plugins you translate, with the same keys, and the catalog's
`translations.<lang>.*` in `catalog/apps.toml`. The installer offers every
language `plugins/installer/locales` has, in its own name; the system's
locale then comes from the language and the time zone
(`core/src/MyArch/Setup/Locales.cs`).

## A plugin

```sh
myarch plugins new my-widget --kind bar    # in the VM
myarch plugins check my-widget             # every theme × every language, and what it can do
```

See [docs/plugin-api.md](docs/plugin-api.md). A plugin says what it
needs (`[packages]`), what it writes (`[[targets]]`), and never writes
outside the person's home except as a hardware plugin (`[hardware]`,
system files only in drop-in folders, off until turned on).

## An app or a profile

`catalog/apps.toml`: an entry per app (Arch's repositories, Flathub, or a
site as an app; no AUR), and profiles as sets of them. The Apps menu and
the installer read it.

## The ISO

`iso/build` takes archiso's own profile and adds `iso/airootfs` (the live
session), `iso/packages.txt` (the live system's packages) and
`iso/target-packages.txt` (the offline repository: everything an install
puts on the disk). An install that stops asking for a package means it's
missing from that last list.
