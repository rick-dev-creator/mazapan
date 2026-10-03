# Contributing

Mazapán is for anyone, anywhere: any language, keyboard, time zone and
machine. Contributions that make it work better somewhere it doesn't yet
are the most welcome.

Code, comments and commit messages are in English; what people see is in
their language.

## Build and test

```sh
core/build test        # the core (needs the .NET 10 SDK and clang): tests, then bin/mazapan
```

Everything that runs Mazapán runs in a VM, never on the machine you work
on (it rewrites your desktop's files):

```sh
vm/vm start            # the dev VM: Arch + Hyprland, this checkout shared at ~/my-arch
vm/vm run 'mazapan apply'
vm/vm shot             # a screenshot of its desktop
```

The ISO is built in the dev VM and tried in a second one, with a blank
disk:

```sh
vm/vm iso              # iso/build in the dev VM: vm/.state/iso/mazapan-*.iso
vm/vm try headless     # boot it (UEFI); vm/vm try start to watch it
vm/vm try shot         # the live desktop; vm/vm try mouse/key to go through the installer
```

After an install, `MAZAPAN_TRY_USER=<the account> vm/vm try ssh` reaches
the installed system (the installer gives it the live system's SSH keys,
which `vm/vm` puts there).

## A language

Every plugin keeps its text in `plugins/<id>/locales/<lang>.toml`
(`en.toml` is required; any other language may leave keys out, and they
show in English). To add a language, add `<lang>.toml` next to `en.toml`
in the plugins you translate, with the same keys, and the catalog's
`translations.<lang>.*` in `catalog/apps.toml`, and `self = "<its name>"`
in `plugins/installer/locales/<lang>.toml`.

The installer offers the world's main languages whether or not Mazapán is
translated to them: the system and its apps speak the one chosen (its
locale comes from the language and the time zone,
`core/src/Mazapan/Setup/Locales.cs`), and Mazapán's own screens show in
English where a translation is missing, which the installer says. Those
with a translation come first; a new one moves up by itself.

## A plugin

```sh
mazapan plugins new my-widget --kind bar    # in the VM
mazapan plugins check my-widget             # every theme × every language, and what it can do
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

## A release

Every release goes to the edge channel first, and to stable once it has
proved itself there. The version comes from git (`pkg/version`): a tag
`vX.Y.Z` is a release, commits after it are `X.Y.Z.rN.gHASH`. What changed
goes in `CHANGELOG.md` under "Unreleased", renamed to the version when
it's tagged (`## X.Y.Z — date`). Before the first tag a build is
`0.0.0.rN`, older than any release. A channel never goes back: a release
older than what it has is refused, and a file once published is never
replaced (a rebuild of the same version keeps the published one).

```sh
core/build test                         # bin/mazapan, with the version in it
vm/vm ssh 'cd my-arch && MAZAPAN_SIGN_KEY=… pkg/release'          # → out/repo/edge
vm/vm ssh 'cd my-arch && MAZAPAN_SIGN_KEY=… pkg/release promote X.Y.Z'   # → stable
```

Before a release is promoted, the gate: the ISO built from it installs
itself unattended in a fresh VM, starts, and every check passes there
(`vm/gate`, encrypted; `vm/gate plain` too). With the test repository
(below) it updates from it as well:

```sh
vm/vm iso                                   # the ISO, as it will be published
vm/gate && vm/gate plain                    # → [gate] PASS the release gate (…)
# With an update from the release's repository (a test build, as below:
# iso/build run in the dev VM with MAZAPAN_REPO_SERVER='file:///srv/mazapan-repo/$channel/$arch'):
MAZAPAN_GATE_REPO=out/repo vm/gate
```

`out/repo` is then uploaded to where `pkg/repository.toml` says. To try a
release without publishing one: a throwaway key (`pkg/release keys`),
`MAZAPAN_KEYS`, `MAZAPAN_VERSION`, `MAZAPAN_REPO_SERVER=file://…` and
`MAZAPAN_RELEASE_DIRTY=1`, as `pkg/release` and `pkg/PKGBUILD` describe.
