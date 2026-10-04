# Plugin API v1

Everything Mazapán does is a plugin, built-ins included: they use exactly
the API described here. If something can't be expressed with it, the API
grows; built-ins never get a private path.

## Where plugins live

```
~/.local/share/mazapan/plugins/<id>/plugin.toml    from git, or your own (searched first)
<repo>/plugins/<dir>/plugin.toml                  built-ins
```

A plugin in the user directory shadows the built-in with the same id (your
own; one from git can't take a built-in's id). Turn plugins on and off with
`mazapan plugins enable|disable <id>…`, which is `disabled_plugins` in
`~/.config/mazapan/config.toml`:

```toml
theme = "phosphor"
disabled_plugins = ["theme-foot"]
```

## Plugins from git

```sh
mazapan plugins add https://github.com/you/mazapan-hello[#ref]   # asks first
mazapan plugins show hello        # requirements, settings, what it can do
mazapan plugins update [hello[#ref]]
mazapan plugins remove hello      # mazapan apply then takes its files away
mazapan plugins sync              # install what plugins.lock says (another machine)
```

`add` clones the repository (its root holds plugin.toml; no symlinks or
submodules) and shows what the plugin will be able to do, worked out from
its manifest, not taken from its word:

- **full access**: code where code runs (QML in the shell, Lua in
  Hyprland, scripts) and configs that can run commands. That is nearly any
  config (foot's `shell=`, hyprlock's `cmd[]`, a `.desktop`'s `Exec=`, VS
  Code's terminal profiles); only stylesheets and color schemes aren't.
- the commands it runs (after writing, as checks, as actions), written out
  as they will run: calls to its shared functions inlined, settings replaced by
  their defaults. So a command is a small language on purpose: text, and
  `{{ … }}` holding literals, the data (`theme.*`, `settings.*`, `home`,
  `plugin`, `lang`, `lang_code`, `place`), `$locals`, operators and the
  functions below (not `t`/`tq`: translated text isn't shown); `if`/`else`;
  and a shared function called on its own (`{{ gsettings }}`), written as
  text (`{{ func gsettings }}…{{ end }}`, no parameters). Anything else
  (`for`, `capture`, `this`, `object.*`, several statements in one `{{ }}`)
  makes the plugin not load: what can't be shown can't be approved.
- the files it writes, the packages it needs.

Nothing is installed until you say yes (`-y` says it without asking;
without a terminal there is no other yes).

`~/.config/mazapan/plugins.lock` keeps, for each one, its source, the branch
or tag it follows, the exact commit, and what you approved. With
config.toml, it's all another machine needs: `mazapan plugins sync && Mazapán
apply`.

`update` looks at the new version beside the installed one, shows its
commits, and asks again only when it needs something you didn't approve;
the plugin moves only then. `mazapan apply` (and doctor, and the rest)
refuses a plugin from git that isn't what the lock says: at another commit,
with any file edited or added (even ignored ones: every `*.tmpl` in the
folder is parsed), needing more than was approved, or a checkout the lock
doesn't list. git runs without your git config or hooks.

No plugin writes into Mazapán's own folders (`~/.config/mazapan`,
`~/.local/share/mazapan` but its `bin/`, `~/.local/state/mazapan`); outputs
start with `~/` or `/` and never go up with `..`. Approving "full access"
is trusting its author, like any extension: that code runs with your
rights.

Your own plugins (a folder you put there, not a git checkout, or a link
`plugins dev` made to one) aren't checked: they're yours.

## Catalogs and the Plugins panel

A catalog lists plugins one can add: only where each lives and what it is.
Adding one is still `plugins add`, with what it can do shown and approved.
Mazapán ships one (`catalog/index.toml`); config.toml adds others, paths or
https URLs (fetched at most once a day, `--refresh` to fetch now):

```toml
catalogs = ["https://example.com/mazapan/index.toml"]
```

```toml
[[plugin]]
id = "bar-uptime"                # its plugin.toml's id
name = "Uptime"
description = "How long the machine has been up, in the bar"
author = "Ana"
source = "https://github.com/ana/mazapan-uptime"
ref = "v1.3"                     # tag or branch; none: the default branch
categories = ["bar"]             # bar, panel, theme, window, hardware, tools, agent
homepage = "https://…"
translations.es.description = "Cuánto tiempo lleva encendido el equipo, en la barra"
```

An id in two catalogs is the first one's. A plugin installed from a catalog
entry (`plugins add ID`) follows its `ref`: when the catalog moves it
(`v1.3` to `v1.4`), `plugins update` goes there, still asking before
anything new, and never to a lower version. A ref you pick yourself
(`plugins update ID#REF`) ends that.

```sh
mazapan plugins catalog [--json]    # every plugin: built in, yours, installed, available
mazapan plugins search TERM…
mazapan plugins preview ID|URL      # its page, without installing it
mazapan plugins add ID              # from a catalog, by id
```

The **Plugins** panel (the `plugin-manager` plugin, SUPER + SHIFT + P) is
all of that with a mouse: search, tabs (installed, available, for this
machine, all), and each plugin's page: its README, what it can do (the
riskiest marked), its settings as controls. It runs the same commands:
turning on and off and settings are `apply --enable|--disable|--set`, so
`mazapan undo` takes them back; installing is `plugins add`, of the very
commit whose capabilities the page showed (after "allow and install"),
and removing, `plugins remove`. What asks for sudo (a hardware plugin) or
an approval (an update needing more) opens in a terminal.

## plugin.toml

```toml
[plugin]
id = "theme-foot"          # unique, stable
name = "foot theme"
version = "0.1.0"
api = 1                    # manifest API; the core refuses other values
description = "one line"
requires = ["shell-bar", "hypr-base >= 0.1"]   # other plugins it needs
categories = ["theme"]     # bar, panel, theme, window, hardware, tools, agent
optional = false           # true: off until turned on (an extra, not everyone's);
                           # its packages are installed then, not with mazapan

[packages]
pacman = ["foot"]          # checked on apply, warned about if missing

[settings]                 # knobs with their defaults (string, int, float, bool)
key = "SUPER + equal"
auto = false

[[targets]]                # zero or more files to generate
template = "foot.ini.tmpl" # file in the plugin directory
output = "~/.config/foot/foot.ini"
reload = "…"               # optional shell command, rendered as a template
```

Unknown keys are an error, so typos don't pass silently. The id is
lowercase letters, digits and dashes, and names the plugin's folder; the
version, numbers and dots. A plugin that doesn't load is listed as broken
and blocks apply (unless disabled), but never the other commands.

`requires` lists plugins this one needs, with an optional version (`>=`,
`>`, `=`, `<=`, `<`; `1.2` is `1.2.0`). Mazapán refuses to apply while an
enabled plugin needs one that is missing, disabled or at a version that
doesn't do; `enable` and `disable` say what else they'd need to take along.
A bar widget requires `shell-bar`; a Hyprland fragment, `hypr-base`.

## Hardware plugins

A plugin with a `[hardware]` table is for some machines only:

```toml
[hardware]            # every rule given must hold; a list holds when any pattern does
vendor = ["LENOVO"]                  # DMI: maker, model (or its version), board
product = ["*Yoga Pro 7 14IAH10*"]
board = ["X870E*"]
pci = ["10de:*"]                     # vendor:device, hex
usb = ["05ac:*"]
gpu = ["NVIDIA*AD1*", "*Radeon*"]    # a GPU's name in the PCI database
input = ["*Synaptics*"]              # an input device's name
modules = ["hid_apple"]              # a loaded kernel module
gpus = 2                             # at least this many GPUs
filesystem = ["btrfs"]               # the root filesystem
bootloader = ["grub"]                # grub, limine, systemd-boot
boot_on_root = true                  # /boot isn't a partition of its own
live = true                          # the ISO's live system (the installer's)
any = true                           # any machine: for all, but it changes the system, so off until asked
```

Patterns are shell patterns, case-insensitive (a literal `[`, common in PCI
names, is `\\[`; a pattern that can't match is an error). Everything is
read from `/sys` and `/proc`, without root; `mazapan hardware` shows the
machine as plugins see it (`--json` for scripts) and which hardware plugins
are for it. Templates see it as `machine` (`vendor`, `product`, `gpus`: each
GPU's `id`, `name`, `driver`), so a plugin can tell a desktop with one GPU
from a hybrid laptop.

A hardware plugin is **off until you turn it on** (`mazapan plugins enable`;
`enabled_plugins` in config.toml). All its rules decide whether it's offered
(`mazapan doctor`, `status --json`'s `hardware_to_offer`); once on, it stays
on while the machine is the same one — maker, model, board, PCI devices,
GPUs — whatever comes and goes (a keyboard unplugged, a module not loaded
yet, a dock's GPU): unplugging the keyboard mustn't take the fix away. On
another machine (a config.toml shared with it) it does nothing. No plugin
can require a hardware plugin.

Most hardware fixes need root. A target with `system = true` is a file
written as root, with sudo, only in a drop-in folder (`/etc/modprobe.d`,
`/etc/modules-load.d`, `/etc/mkinitcpio.conf.d`, `/etc/udev/rules.d`,
`/etc/udev/hwdb.d`, `/etc/sysctl.d`, `/etc/tmpfiles.d`,
`/etc/systemd/logind.conf.d`, `/etc/systemd/sleep.conf.d`,
`/etc/X11/xorg.conf.d`, browsers' `policies/managed`,
pacman's hooks (`/etc/pacman.d/hooks`), mkinitcpio's (`/etc/initcpio/post`),
`snapper-cleanup.service.d`,
`/etc/grub.d`, and for the login screen
`/etc/greetd`, `/etc/systemd/system/greetd.service.d` and `/etc/pam.d`,
`/etc/systemd/system/ufw.service.d`, `tailscaled.service.d` and
`docker.socket.d`, a service
of its own) and named `mazapan*`, or one of a short list of overrides of a
package's file kept in /usr/lib (`/etc/pam.d/polkit-1`, Plugin.SystemOverrides):
never a file the system or another package owns. Its `reload` runs as
root; `reboot = true` says it takes effect after a reboot. `[packages]
pacman` are installed too.

None of that happens on a plain `mazapan apply` (the theme picker's, an
agent's): it says what waits. `mazapan apply --system` lists everything it
will do as root — packages, each file's diff, removals, commands — and asks
before the first sudo (`-y` to skip the question; without a terminal it's
required). Then, in order: packages (a driver before the files that load
it), checks marked `before_system = true` (kernel headers before a module
built from them), files, and their reloads — also those of files it
removes, and of files undo puts back, which Mazapán remembers. `mazapan undo`
takes it all back: system files as they were, packages it installed
uninstalled unless something else needs them by now.

What's written as root comes from the plugin: a plugin with system files
has only number and true/false settings (text, which any program can put in
config.toml, never reaches a root file). Agents (`mazapan mcp`) can't turn
hardware plugins on or off, change their settings, apply as root, or undo
what was done as root. Every root write, delete and package name is checked
where sudo runs, whatever asked for it: owned.json and snapshots are yours
to edit, so a path in them is never enough.

## Checks

A plugin says how to tell that what it's responsible for still works:

```toml
[[checks]]
name = "{{ t \"check.config\" }}"    # rendered: can be translated
run = "Hyprland --verify-config -c ~/.config/hypr/hyprland.lua"
timeout = 15                             # seconds, default 15
session = false                          # needs the graphical session?
```

`run` is a shell command (rendered as a template, like `reload`); exit 0
means healthy, and its output is shown when it fails. `mazapan doctor` runs
every check; `mazapan update` runs them after updating and rolls the update
back when one fails. A check that needs the graphical session (Hyprland,
the bar, the user's PipeWire) sets `session = true`: outside it (a TTY,
SSH) it's skipped, never failed, so it can't roll back a good update.

Checks run one after the other and may fix what they look at on the way,
as long as they report the result: the bar's check restarts the bar when
Quickshell was updated underneath it, then checks it loaded.

## Actions

What a plugin lets you do, for the command palette (`SUPER + Space`):

```toml
[[actions]]
name = "{{ t \"open\" }}"                         # rendered: translatable
run = "qs ipc -c mazapan call mazapan panel monitors"   # the command it runs
key = "{{ settings.key }}"                         # its keybinding
terminal = false                                      # run it in a terminal
keywords = "displays screens resolution"              # other words for it
```

`run` is a shell command, and the palette shows it next to the action: the
point is that people learn it. When the plugin's keybinding calls a Lua
function, expose that function as a global and make `run` call it through
`hyprctl eval` (see `columns`: `mazapan_columns.equal()`), so the key and
the command do exactly the same thing. An action with a `key` and no `run`
is a keybinding that only makes sense as a key ("SUPER + 1…0"). Every
keybinding a plugin binds should be one of its actions: that's how the
palette's list of keys stays right. `terminal = true` is for commands that
ask or print (`mazapan update`); the terminal stays open afterwards.

The palette opened with nothing typed is the desktop's menu: a tile for
each place, then the power row and the open windows. A plugin puts its way
in there (its panel, not each thing it does: those are found by typing):

```toml
home = 4                       # on the first screen, in this order (0: not)
glyph = "󰸌"                    # its icon there (a Nerd Font glyph)
label = "{{ t \"home\" }}"     # its name there, short ("Theme"); default: name
confirm = true                 # done only on a second ↵ (power off, reboot)
```

A `key` written as one of the plugin's settings (`key = "{{ settings.key }}"`)
can be changed in Settings › Keys, by pressing the new keys; a fixed one is
only shown there.

## Coverage

What a plugin themes, so `mazapan coverage` (and the theme picker) can tell
which installed apps the theme doesn't reach:

```toml
[coverage]
apps = ["foot", "footclient"]   # .desktop ids or executable names
toolkits = ["terminal"]         # terminal, gtk4, gtk3, qt6, qt5,
                                # electron, chromium, firefox,
                                # flatpak, web (web apps)
```

An app counts as covered when a plugin names it, or names its toolkit.

## Settings

A plugin declares its settings with defaults; people override them in
`~/.config/mazapan/config.toml`:

```toml
[plugins.columns]
auto = true
min_width = 400
```

A setting is a default alone, described by the comment above it; or a
table that says more, which the Plugins panel turns into the right control
and `apply` enforces:

```toml
[settings]
# Show seconds too.
seconds = false                                   # a switch
# How often to refresh it, in seconds.
refresh_seconds = { default = 60, min = 10, max = 3600, step = 10 }
units = { default = "metric", choices = ["metric", "imperial"], label = "Units" }
folder = { default = "Pictures", kind = "path" }
```

`kind` is `text`, `number`, `integer`, `switch`, `choice`, `key`, `command`,
`color`, `path`, `font` or `list`; left out, it's taken from the default
(and the name: `key`, `*_key` are keys; `terminal`, `*_command` commands).
The label is the key made readable (`font_size`: "Font size") unless given;
both are translated with `setting.KEY` and `setting.KEY.help` in the
locales.

Overrides must use a key the plugin declares and the same type as the
default (an integer is accepted where the default is a float). Anything
else, including a `[plugins.<id>]` section for a plugin that doesn't exist,
stops `apply` with an error naming the section. `mazapan plugins` prints each
plugin's effective settings and marks the ones set in config.toml.

## Localization

Plugins never hardcode user-facing text in their templates. It lives in
`locales/<lang>.toml`, flat `key = "text"` tables:

```
plugins/bar-weather/locales/en.toml   required, the fallback
plugins/bar-weather/locales/es.toml   may leave keys out
plugins/bar-weather/locales/es_MX.toml
```

The language comes from the OS (`LC_ALL`, `LC_MESSAGES`, `LANG`, then
`/etc/locale.conf`), or from `language = "es"` in config.toml. For `es_MX`
the core tries `es_MX.toml`, `es.toml`, then `en.toml`, key by key. A key
in another language that en.toml doesn't have stops `apply` (it's a typo);
a key missing everywhere fails the render. What plugin.toml already says
in English needs no en.toml key: `plugin.name`, `plugin.description`,
`setting.KEY` and `setting.KEY.help` translate the name, description and
settings for the Plugins panel. A `README.es.md` (or `README.es_MX.md`) is
the page in that language; `README.md` the rest.

In templates, `t "key"` gives the text and `tq "key"` gives it as a quoted
literal that is valid in QML/JS and Lua. Placeholders are `%1`, `%2`… and
filled in with QML's `.arg()`: `{{tq "updated"}}.arg(time)`. `lang`
(`es_MX`) and `lang_code` (`es`) are there for APIs that take a language,
and for `Qt.locale(…)`, which gives day names and time formats for free.

## Targets

The core renders every target and writes it; plugins never touch the disk.
That gives three guarantees:

- **Ownership.** The core records a hash of each file it writes
  (`~/.local/state/mazapan/owned.json`). A file it didn't write, or one a
  person edited afterwards, is a *conflict*: `apply` stops and lists it.
  `apply --adopt` backs it up (`*.mazapan-bak-<time>`) and takes it over.
- **Clean removal.** When a plugin is disabled, its files are deleted on the
  next `apply`, unless someone edited them; those are left in place and
  released.
- **One owner per path.** Two plugins generating the same output is an error.
- **Files shared with their app.** Some apps write their own config file
  too (qt6ct saves its window geometry, KDE apps their settings in
  kdeglobals, a browser its Preferences), and some files are often the
  person's (Firefox's user.js, userChrome.css). A target with `merge` is
  shared: the core manages only what the template renders, merges it into
  the file and keeps the rest; only a change to one of its keys is a
  conflict, and a person's own value for one the first time. When the
  plugin is disabled, the file stays. Formats: `ini`; `prefs` (user.js's
  `user_pref("name", value);` lines); `lines` (the template's lines must be
  at the head of the file, missing ones go on top: an `@import`); `json`
  (the template's leaves in a JSON object; one that doesn't parse is a
  conflict, never rewritten). `prefs` and `lines` are the person's own
  files: taking one of Mazapán's lines out is an edit (a conflict, not put
  back), and Mazapán's lines leave with the plugin (or when a newer version
  stops writing them).
- **In every place.** `each = ["~/.config/mozilla/firefox/*/prefs.js"]`
  writes the target into every directory holding a file those patterns
  match (every browser profile, symlinks counted once), `output` being
  relative to it; templates see the directory as `.Place`. New places are
  taken on the next apply; a rollback doesn't bring back a deleted one.
  `busy = "../SingletonLock"`: while that file exists (relative to the
  place), the app is running and would write its own copy back, so the
  file is left for the next apply (`busy` in the plan).

After writing, each distinct `reload` command of plugins whose files changed
runs once. A failing reload is a warning: the files are already in place.
A file that's removed (its plugin turned off or gone) runs the `reload` it
was last written with, once, after it's gone: a reload that checks whether
its file is there undoes what it did (a service enabled while its unit
exists is stopped), as system files' reloads do.

## Templates

[Scriban](https://github.com/scriban/scriban): `{{ theme.meta.mode }}`,
`{{ if settings.auto }}…{{ end }}`, `{{ for f in under "…" }}…{{ end }}`,
`{{ c "accent" }}`; `{{-` and `-}}` take the whitespace next to them. A
missing name or token fails the render instead of producing an empty
value, and every name below is read-only: a template can't change what the
next one sees.

A plugin's `_*.tmpl` files hold functions shared by its templates and
commands, and nothing else:

```
{{- func named_colors -}}
@define-color accent_color {{ c "accent_text" }};
{{- end -}}
```

A template calls it as `{{ named_colors }}`; a command, the same way on its
own (`reload = "{{ gsettings }}"`), which is how its capability can show
what it runs (see `theme-gtk`). A function can take parameters:
`{{ func roles(role) }}…{{ end }}`, called as `{{ roles "fg" }}`.

Each template gets 10 seconds, then the render fails: a plugin from git
whose template loops forever can't hang Mazapán. A template gets the data
made anew: what it changes (an item of `actions`, say) no other template,
plugin or command sees. A template is a `*.tmpl` file in the plugin's own
folder, never a path out of it. Scriban's functions are all there but those
that read or run something else (`include`, `object.eval`) or change on
every apply (`date.now`, `math.random`).

A text setting that goes into code is always quoted for it (`lq` for Lua,
`quote` for QML/JS, `shq` for a shell command, `inline` for a comment or an
ini value): its value must never be able to close a string and add code.
`shq` makes it one shell word, nothing expanded (`$`, backticks, globs);
`quote` in a shell command would still expand `$(…)`.

What templates see:

| Name           | Meaning                                         |
|----------------|-------------------------------------------------|
| `theme`        | the theme: `id`, `meta` (`name`, `mode`, `accents`), `font` (`mono`, `ui`, `size`), `shape` (`radius`, `border`, `gap_in`, `gap_out`), `effects` (`terminal_opacity`, `blur`, `wallpaper`), `motion` (`enabled`, `duration_ms`, `curve`, `response_ms`, `damping`), `colors`, `ansi` |
| `plugin`       | this plugin's id                                |
| `home`         | the user's home directory                       |
| `settings`     | the plugin's settings, defaults merged with config.toml |
| `lang`         | the language, as a POSIX locale name: `es_MX`, `en` |
| `lang_code`    | just the language: `es`, `en`                   |
| `actions`      | every plugin's actions, rendered: `plugin`, `name`, and `run`, `key`, `terminal`, `keywords` when set |
| `place`        | for a target with `each`: the folder this copy goes into |

Functions, besides Scriban's own (`string.*`, `array.*`, `object.keys`…):

| Function                   | Example result                |
|----------------------------|-------------------------------|
| `c "accent"`               | `#8a5cf5` (colors, then ansi) |
| `hex (c "accent")`         | `8a5cf5`                      |
| `rgb (c "accent")`         | `rgb(8a5cf5)` (Hyprland)      |
| `rgba (c "accent") 0.5`    | `rgba(8a5cf580)` (Hyprland)   |
| `cssa (c "accent") 0.36`   | `rgba(138, 92, 245, 0.36)`    |
| `csv (c "accent")`         | `138,92,245` (KDE)            |
| `speed 0.6`                | Hyprland speed for 60% of the theme duration |
| `spring 1.1 1`             | `mass = 1, stiffness = …, dampening = …`: the theme's spring with its response ×1.1 and damping 1 (0 keeps the theme's) |
| `num 11.0`                 | `11`                          |
| `pct 0.9`                  | `90`                          |
| `base place`               | `gwfdp8rp.default-release`    |
| `mix (c "bg") (c "success") 0.18` | the second over the first at 18%: `#2a3322` |
| `camel "bg_alt"`           | `bgAlt` (QML property names)  |
| `t "today"`                | `hoy` (this plugin's text)    |
| `tq "today"`               | `"hoy"` (quoted for QML/Lua)  |
| `quote settings.format`    | any string, quoted the same way (QML/JS) |
| `shq settings.folder`      | one word in a shell command, nothing expanded |
| `lq settings.key`          | any string as a Lua literal: `"SUPER + space"` |
| `inline settings.style`    | without line breaks: for a comment or an ini value |
| `under "~/.config/hypr/mazapan/"` | every plugin output below that path |
| `json actions`             | a JSON value, also a valid QML/JS literal |

`under` is how an entry point includes fragments without knowing which
plugins exist: `hypr-base` loads every file other plugins generate under
`~/.config/hypr/mazapan/`, each one isolated so a broken fragment can't stop
the rest from loading.

## Theme tokens

The token names in `core/src/Mazapan/Themes/Theme.cs` (`RequiredColors`,
`RequiredAnsi`) are part of this API. Ask for meaning (`danger`,
`surface_raised`), not for a hue; `[ansi]` is for terminals and TUIs.

The accent tokens (`accent`, `accent_fg`, `accent_text`, `accent_deep`,
`selection`) may not be the theme's: when the person picks another accent
(`mazapan apply --accent`, the theme picker), they're derived from it with
their contrast kept. Rely on what each one is for, not on its value.

The theme picker previews themes live: QML that reads `Theme.<token>` in
bindings morphs with it for free. A color copied into a plain value
(`property string c: "" + Theme.accent`) doesn't follow; Hyprland-side
colors follow through `mazapan_theme({...})` (plugin `theme-hyprland`).

A theme's `[effects]` are optional: `terminal_opacity` (0.5–1),
`blur` (see-through windows blur what's behind), and `wallpaper`: `"grid"`
(drawn from the tokens), `"plain"` (bg_alt) or an image next to
theme.toml. Templates read them as `theme.effects.terminal_opacity`,
`.blur`, `.wallpaper`; QML as `Theme.terminalOpacity`, `Theme.blur`,
`Theme.wallpaper`, `Theme.mode`.

A theme can suggest accents besides its own, in `[meta]`:
`accents = ["#d99a2b", "#4fa35f"]`. `mazapan themes` lists every theme with
the contrast pairs it fails (WCAG: 4.5:1 for text, 3:1 for fills).

## Bar widgets

The bar (plugin `shell-bar`) has no widgets of its own. A plugin adds one by
generating a QML file into a slot:

```
~/.config/quickshell/mazapan/widgets/left/<NN>-<name>.qml
~/.config/quickshell/mazapan/widgets/center/<NN>-<name>.qml
~/.config/quickshell/mazapan/widgets/right/<NN>-<name>.qml
```

`NN` orders widgets within a slot. Each one loads on its own: one that
fails shows a warning in its place and the rest keep working. The root item
needs an `implicitWidth`; it may declare `property var barScreen` and
`property int barHeight`, set once it loads, and `property bool shown`: a
widget with nothing to show sets it to false and the bar takes it out, gap
included, until it's true again (use this, not `visible`). One whose text
can be shortened (a window's title) declares `readonly property bool
elastic: true` and follows its `width`: when the bar is short of room
(the center moves aside rather than overlap the right), it gives up room
first. Shared, non-widget files (a
data service, a component) go under `components/<name>/`.

After writing, a widget plugin asks the running shell to reload with
`reload = "sh ~/.local/share/mazapan/bin/shell-reload"` (generated by
`shell-bar`; it also starts the shell if it isn't running).

Panels are windows that aren't part of the bar (the monitor manager, a
launcher): a plugin generates `~/.config/quickshell/mazapan/panels/<name>.qml`,
whose root item has `function toggle()`, `property bool open` and
`function close()`, and creates its own windows. The shell loads it once;
`qs ipc -c mazapan call mazapan panel <name>` opens or closes it (bind that to
a key from the plugin's Hyprland fragment). A panel window that takes the
keyboard uses the layer namespace `mazapan-panel-<name>`: then SUPER + Q
closes it, like any window (`qs ipc -c mazapan call mazapan close` closes
every open panel), instead of closing the window behind it. Panels import
the kit as `"../components/kit"`.

The shell and some plugins answer other plugins through IPC, so one can
drive another without depending on it (a missing one just doesn't
answer). Those a mode uses: `mazapan hide "markets,weather"` takes widgets
out of the bar (by file name without `NN-` and `.qml`; `""` puts them
back), `mazapan widgets` lists them; `notifications setMode NAME QUIET
"app,app"` is a mode's Do Not Disturb and the apps it lets through;
`nightlight hold on|off|none`; `idle` (the helper at
`~/.local/share/mazapan/bin/idle awake on|off`).

`shell-bar` ships a kit for widgets, `import "../../components/kit"`:

| Component  | What it is                                                     |
|------------|----------------------------------------------------------------|
| `BarItem`  | the clickable part of a widget: hover/open highlight, `clicked`, `wheel` |
| `BarPopup` | the card that drops down from it: `toggle()`, closes on click outside or Escape, `opening`, `key` |
| `Glyph`    | a Nerd Font icon                                               |
| `Label`    | body text                                                      |
| `Caption`  | a section label                                                |
| `Rule`     | a hairline between sections                                    |
| `Toggle`   | an on/off switch: `checked`, `toggled(bool)`                   |
| `Slider`   | `value`, `to`, `step`, `moved(real)`                           |
| `ListRow`  | a row in a list: `glyph`, `title`, `detail`, `trailing`, `active`, `clicked(mouse)` |
| `Button`   | `text`, `primary`, `clicked`                                   |
| `TextField`| `text`, `placeholder`, `echoMode`, `accepted`, `focusInput()`  |

They all take their colors, font and shape from the theme. A
`TextField`'s own `Keys` (`Keys.onPressed` on it) see a key before the
text does: what they accept (the arrows of a list, Delete for an entry,
Esc) never reaches the text; the rest is typed.

## Writing a plugin

```sh
mazapan plugins new bar-uptime --kind bar   # bar, panel, window, theme, tools
mazapan plugins dev bar-uptime              # applied again on every save
mazapan plugins check bar-uptime            # before sharing it
```

`new` writes a working plugin of that kind to start from: its manifest with
described settings, a template, en and es locales, a README. It goes to the
plugin folder, on at once, or to `--dir ~/src/bar-uptime` for a repository
of its own.

`dev` watches the plugin's files and applies on every save until Ctrl+C:
template errors in the terminal, and for QML, what the shell says when it
loads the file (a syntax error, a property that isn't there). A folder
elsewhere is linked into the plugin folder first, and stays linked
(`plugins remove ID` takes the link, not the folder). Its applies keep no
undo snapshots: undo's history stays yours.

`check` renders the plugin with every theme, in every language it has,
with its settings' defaults, and tells what's wrong (errors: it fails) and
what's missing (warnings): a description, categories, a README, settings
without a description, text still in English in a language it has. Then
what it would be able to do, as `plugins add` will show it.

`fork ID` copies a built-in plugin into the plugin folder, where it takes
the built-in's place; `plugins diff ID` shows what you changed, and deleting
the copy goes back. `fork ID NEW` copies any plugin under a new id, to start
from.

To share it: a git repository with plugin.toml at its root, tagged, and an
entry in a catalog (a pull request to Mazapán's `catalog/index.toml`, or
your own catalog file).

