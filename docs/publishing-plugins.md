# Publishing a plugin

Anyone can make a plugin for Mazapan and share it with everyone who uses
it. This is the whole way there, from an empty folder to your plugin in
every Mazapan's Plugins panel and on [mazapan.dev/plugins](https://mazapan.dev/plugins/),
and what happens after: new versions, changes, taking it down.

How a plugin works (its manifest, templates, settings, the shell's
widgets) is in [plugin-api.md](plugin-api.md). This page is about
publishing one.

## The flow at a glance

```
your repository                    the registry                         everyone
───────────────                    ────────────                         ────────
1. mazapan plugins new …
2. mazapan plugins dev …  (write it)
3. mazapan plugins check …  ✓
4. push to a public repository
5. tag v1.0.0 ──────────────────▶ 6. pull request: your entry
                                   7. automatic checks + a person's review
                                   8. merged ──────────────────────────▶ 9. mazapan.dev/plugins,
                                                                            the Plugins panel,
                                                                            mazapan plugins add
10. tag v1.1.0 ─────────────────▶ 11. found within 6 hours:
                                      nothing new it can do → listed ──▶ 12. the Updates panel
                                      it can do more → a pull request,
                                      reviewed, then listed
```

Your plugin's code always stays in **your** repository: its issues, its
releases, its README are yours. The registry,
[rick-dev-creator/mazapan-plugins](https://github.com/rick-dev-creator/mazapan-plugins),
only says where it lives and which version is listed: a tag, and that
tag's exact commit. What the registry looked at is exactly what people
install, even if the tag is moved later.

## What you need

- Mazapan, to write and test the plugin (a computer with it, or the dev
  VM: [development.md](development.md)): 0.4.0 or newer, the first that
  reads the plugin registry. So do the people who install it.
- git, and an account on GitHub, or any host that serves public
  repositories over `https://`.

## 1. Start the plugin

```sh
mazapan plugins new my-plugin --kind panel --dir ~/src/mazapan-my-plugin
```

That writes a working plugin of that kind to start from: `plugin.toml`,
a template, English and Spanish texts, a README. The kinds are `bar` (a
widget in the bar), `panel` (a panel that opens over the desktop),
`window` (Hyprland rules and layouts), `theme` (an app in the theme) and
`tools` (anything else).

To start from one of Mazapan's own plugins instead:

```sh
mazapan plugins fork bar-clock my-clock          # a copy under a new id, in the plugin folder
mv ~/.local/share/mazapan/plugins/my-clock ~/src/mazapan-my-clock
```

**The id** (`my-plugin`) is lowercase letters, digits and dashes. It names
the plugin everywhere (its folder, its settings in config.toml), so pick
it well: it can't change once people have the plugin. It must not be the
id of a plugin Mazapan ships (`mazapan plugins` lists them) or of one
already in the registry. **The repository's name** is yours to pick;
`mazapan-<id>` is the custom (`mazapan-pomodoro`).

## 2. Write it

```sh
mazapan plugins dev ~/src/mazapan-my-plugin
```

`dev` links your folder into Mazapan's plugin folder, turns it on and
applies it again on every save, until Ctrl+C: template errors in the
terminal, and for QML, what the shell says when it loads the file. Keep it
running while you work. Everything a plugin can do, and how, is in
[plugin-api.md](plugin-api.md); Mazapan's own plugins (`plugins/` in this
repository) and [mazapan-pomodoro](https://github.com/rick-dev-creator/mazapan-pomodoro)
are complete examples.

Say what it is in every language you can: `locales/<lang>.toml` with
`plugin.name`, `plugin.description` and each setting's label and help.
The Plugins panel and the gallery show it in each person's language.

## 3. Make it ready to share

Besides working, a plugin others can add says who made it and how it
looks. In `plugin.toml`:

```toml
[plugin]
id = "my-plugin"
name = "My plugin"
version = "1.0.0"
api = 1
description = "One line on what it does"
categories = ["panel"]        # bar, panel, theme, window, hardware, tools, agent
author = "Your name"
homepage = "https://github.com/you/mazapan-my-plugin"
license = "MIT"

[gallery]
icon = "media/icon.svg"              # SVG, PNG, JPEG or WebP, up to 256 KB
screenshots = ["media/panel.webp"]   # PNG, JPEG or WebP, up to 2 MB each, up to 8
```

And next to it:

- **README.md**: what it does and how to use it (its keys, its settings).
  It's the plugin's page in the Plugins panel and on mazapan.dev.
- **LICENSE**: what others may do with your code (MIT, GPL, …).
- **The pictures** `[gallery]` names. Screenshots of the whole screen
  read best (1920×1080, WebP keeps them small); a square icon.

Then check it:

```sh
mazapan plugins check ~/src/mazapan-my-plugin
```

`check` renders your plugin with every theme, in every language it has,
with its settings' defaults, and says what it would be able to do (as
people will see it before they install it). **Errors** make it fail and the
registry refuses it; **warnings** are worth fixing too. It's what the
registry runs on your plugin, with a few more rules:

| The registry asks | Why |
|---|---|
| `mazapan plugins check` with no errors | It loads, and works with every theme and language |
| An id that's yours: not one of Mazapan's, not listed yet | One id, one plugin, everywhere |
| No key (`SUPER + …`) one of Mazapan's own plugins uses | Both would fire (`check` says which) |
| A `vX.Y.Z` tag that is `version` in plugin.toml, at the commit listed | What's listed is what's installed |
| README.md, LICENSE, `author`, `[gallery] icon` | Its page, and what others may do with it |
| No symlinks or submodules | Mazapan refuses them: nothing from outside the commit |

## 4. Put it in a repository

```sh
cd ~/src/mazapan-my-plugin
git init -b main
git add -A && git commit -m "My plugin 1.0.0"
git remote add origin https://github.com/you/mazapan-my-plugin.git
git push -u origin main
```

The repository must be **public**, with `plugin.toml` at its root.

Add the registry's check to your CI, so every push and pull request is
checked the way the registry will check it:

```yaml
# .github/workflows/check.yml
name: Check
on:
  push:
  pull_request:
  workflow_dispatch:
jobs:
  check:
    uses: rick-dev-creator/mazapan-plugins/.github/workflows/check-plugin.yml@main
```

It builds Mazapan from source, runs `plugins check`, and on a version tag,
checks that the tag is plugin.toml's version.

## 5. Release a version

A version is a tag, `v` and the version in plugin.toml:

```sh
git tag v1.0.0
git push origin v1.0.0
```

Try it as people will get it, from the tag, on a computer (or a VM) where
your `dev` link isn't (or after `mazapan plugins remove my-plugin`):

```sh
mazapan plugins preview https://github.com/you/mazapan-my-plugin#v1.0.0   # its page, and what it can do
mazapan plugins add https://github.com/you/mazapan-my-plugin#v1.0.0       # asks, then installs
mazapan apply
```

People can already install it this way, by its address. The registry is
what makes it findable: in the Plugins panel, `mazapan plugins add
my-plugin`, mazapan.dev/plugins.

## 6. List it in the registry

1. Fork [rick-dev-creator/mazapan-plugins](https://github.com/rick-dev-creator/mazapan-plugins).
2. Add your entry to `plugins.toml`, in order by id:

   ```toml
   [[plugin]]
   id = "my-plugin"
   source = "https://github.com/you/mazapan-my-plugin"
   ref = "v1.0.0"
   commit = "…"   # git rev-parse v1.0.0^{commit}, the full 40 characters
   ```

   Only these four keys: everything else (name, description, author,
   translations, icon, screenshots) is read from your plugin at that
   commit.
3. Open a pull request.

Then:

- **The checks run on your pull request**: your plugin is fetched at that
  commit and checked as in the table above. Their log says what was found
  and everything the plugin would be able to do. Fix what fails in your
  repository, tag again (a new version: `v1.0.1`), and update your entry.
- **A person reviews it**: what it can do against what it says it does,
  and its code. Listing a plugin is the registry vouching that it is what
  it says it is. Plugins that read your files or the network, or run
  commands, are fine when that's what they're for and the README says so.
- **Merged, it's listed**: the registry's catalog and gallery are made
  again at once, and mazapan.dev picks them up within the hour. Mazapan
  reads the catalog at most once a day (`mazapan plugins search --refresh`
  reads it now): your plugin is then in the Plugins panel, under the
  community's, and `mazapan plugins add my-plugin` installs it.

## 7. New versions

You don't open a pull request for each version. Raise `version` in
plugin.toml, commit, and tag:

```sh
git tag v1.1.0 && git push origin v1.1.0
```

Every six hours the registry looks for a newer `vX.Y.Z` tag in each listed
repository, and checks it like a new entry:

- **It can do nothing the listed version couldn't**: it's listed by
  itself, and the gallery shows it. People who have the plugin see the
  update in the Updates panel (`mazapan plugins update` installs it).
- **It would be able to do more** (another file, another command, a
  package): the registry opens a pull request that says what's new, and a
  person reviews it before it's listed. People who have the plugin are
  asked again in Mazapan to approve what's new before it updates.
- **It doesn't pass the checks**: it isn't listed, and the listed version
  stays. Your CI shows why.

The rules for versions:

- **Never move or delete a tag** once pushed. The registry and every
  installed copy hold the commit; a moved tag isn't followed, and the next
  version is picked from newer tags only.
- **Never reuse a version number**, and only go up: Mazapan never updates
  a plugin to a lower version.
- Only `vX.Y.Z` tags count; `v1.2.0-rc1`, `beta` and the like are ignored,
  so you can tag test builds freely.

## Changing it, or taking it down

- **The repository moved** (renamed, another owner): a pull request
  changing `source` in your entry.
- **Another id**: it's another plugin (people's settings are under the
  old id). List it as new, and take the old one down.
- **Taking it down**: a pull request removing your entry. It disappears
  from the panel and the gallery; copies already installed keep working,
  with no more updates.
- **A security problem** in a listed plugin, yours or someone's: open an
  issue in the registry, or, if it shouldn't be public yet, follow
  [SECURITY.md](../SECURITY.md).

## When the checks fail

| It says | What to do |
|---|---|
| `the tag vX is at …, the entry says …` | The commit in your entry isn't the tag's: `git rev-parse vX^{commit}` |
| `its plugin.toml says version …, its tag …` | Tag `v` + plugin.toml's `version`, exactly |
| `the repository's plugin is "…"` | The entry's id must be plugin.toml's `id` |
| `… is a plugin Mazapan ships` | Pick another id |
| `its key … is …'s too` | Use another key combination (or make it a setting with another default) |
| `no LICENSE file` / `no README.md` / `no author` / `no icon` | Add them (see step 3) |
| `[gallery] … is … KB: up to …` | A smaller picture (WebP, or fewer pixels) |
| `… can't be read: is the repository public?` | Make it public, check the address in `source` |
| `it has a symlink` / `a submodule` | Commit the files themselves |
| A theme or a language fails to render | Run `mazapan plugins check` yourself: it says which and where |

## Checklist

- [ ] `mazapan plugins check` passes, with no warnings you can fix
- [ ] plugin.toml: `author`, `license`, `homepage`, `categories`, `[gallery]`
- [ ] README.md, LICENSE, the icon and screenshots in the repository
- [ ] The repository is public; CI runs the registry's check
- [ ] Tagged `vX.Y.Z`, the same as `version`, and pushed
- [ ] Tried from the tag: `mazapan plugins add <url>#vX.Y.Z`
- [ ] A pull request to the registry with `id`, `source`, `ref`, `commit`
