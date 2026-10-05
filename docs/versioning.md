# Versions

One source: **the git tag `vX.Y.Z`** on `main`. Everything else is derived
from it by a script, never typed by hand.

## Mazapan's version

[Semantic Versioning](https://semver.org): `X.Y.Z`.

| Part | Goes up when | Example |
|---|---|---|
| X (major) | something people set up stops working as it did: `config.toml`, the plugin API (`api = N` in plugin.toml), a command's meaning | 1.0.0 → 2.0.0 |
| Y (minor) | something new: a feature, a plugin, a profile | 0.1.0 → 0.2.0 |
| Z (patch) | fixes only | 0.1.0 → 0.1.1 |

While X is 0, a minor version may also change what's there (it's a beta);
1.0.0 promises the rest.

`pkg/version` says the version of any commit:

| Commit | Version | Published |
|---|---|---|
| tagged `v0.1.0` | `0.1.0` | yes |
| 5 commits after it | `0.1.0.r5.gabc1234` | no (a build between releases) |
| before the first tag | `0.0.0.rN.gHASH` | no |

A tag is `vX.Y.Z` only: no `-rc1` or other suffixes (pacman can't order
them). A version is never tagged twice or moved; a mistake is the next patch.

## Its name

Each minor version has a name, the same for its patches, shown after the
number (`mazapan --version`: `mazapan 0.1.0 (Mazapan)`). The list is
`core/src/Mazapan/Updates/Codename.cs`; the next one is added there
before its version is tagged.

| Version | Name |
|---|---|
| 0.1 | Mazapan |

## Where the version goes

| What | Takes it from | Looks like |
|---|---|---|
| `mazapan --version` | core/build ← pkg/version | `mazapan 0.1.0 (Mazapan)` |
| the package | pkg/PKGBUILD ← pkg/version (checked against the binary) | `mazapan-0.1.0-1-x86_64.pkg.tar.zst` |
| the repository | pkg/release: every release to `edge`, `promote` to `stable` | `stable/x86_64/mazapan.db` |
| the ISO | iso/build ← pkg/version, and the day its Arch packages were taken | `mazapan-0.1.0-2026.10.05-x86_64.iso` |
| SourceForge | pkg/sourceforge ← the ISO's name (refuses anything but a release's) | `files/0.1.0/mazapan-0.1.0-2026.10.05-x86_64.iso` |
| CHANGELOG.md | the release's section, its title the version, name and date | `## 0.1.0 — Mazapan (2026-10-20)` |

## The ISO

Mazapan rolls (on Arch): an installed system is never "on 0.1"; it updates.
The ISO is a way in, dated by the Arch it carries:

- a new one with each release, and one a month in between (the same version,
  a new date), so it's never far behind;
- the last two or three on the download servers; the older ones archived
  (a torrent, the Internet Archive), with their SHA-256 and signature kept
  for good.

## The package repository

Installed systems update Mazapan's own packages from
`https://repo.mazapan.dev/$channel/$arch` (GitHub Pages: the repository
rick-dev-creator/mazapan-repo), Arch's from Arch. `pkg/repository.toml`
names it; it stays empty until that server has a signed database, since
pacman fails on a repository it can't read (an install from the ISO too).
The first release sets it, in the same commit that's tagged.

## Making a release

1. CHANGELOG.md: "Unreleased" becomes `## X.Y.Z — Name (date)`.
2. The name in Codename.cs, if it's a new minor version.
3. `git tag vX.Y.Z` on main, pushed to Gitea and GitHub.
4. `core/build test`, `pkg/release` (edge), the ISO (`vm/vm iso`), the gate
   (`vm/gate`, encrypted and plain).
5. `pkg/release promote X.Y.Z` (stable), `pkg/sourceforge` the ISO.
