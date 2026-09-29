# Plugin API v1

Everything myarch does is a plugin, built-ins included: they use exactly
the API described here. If something can't be expressed with it, the API
grows; built-ins never get a private path.

## Where plugins live

```
~/.local/share/myarch/plugins/<id>/plugin.toml    from git, or your own (searched first)
<repo>/plugins/<dir>/plugin.toml                  built-ins
```

A plugin in the user directory shadows the built-in with the same id (your
own; one from git can't take a built-in's id). Turn plugins on and off with
`myarch plugins enable|disable <id>…`, which is `disabled_plugins` in
`~/.config/myarch/config.toml`:

```toml
theme = "phosphor"
disabled_plugins = ["theme-foot"]
```

## Plugins from git

```sh
myarch plugins add https://github.com/you/myarch-hello[#ref]   # asks first
myarch plugins show hello        # requirements, settings, what it can do
myarch plugins update [hello[#ref]]
myarch plugins remove hello      # myarch apply then takes its files away
myarch plugins sync              # install what plugins.lock says (another machine)
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

`~/.config/myarch/plugins.lock` keeps, for each one, its source, the branch
or tag it follows, the exact commit, and what you approved. With
config.toml, it's all another machine needs: `myarch plugins sync && myarch
apply`.

`update` looks at the new version beside the installed one, shows its
commits, and asks again only when it needs something you didn't approve;
the plugin moves only then. `myarch apply` (and doctor, and the rest)
refuses a plugin from git that isn't what the lock says: at another commit,
with any file edited or added (even ignored ones: every `*.tmpl` in the
folder is parsed), needing more than was approved, or a checkout the lock
doesn't list. git runs without your git config or hooks.

No plugin writes into myarch's own folders (`~/.config/myarch`,
`~/.local/share/myarch` but its `bin/`, `~/.local/state/myarch`); outputs
start with `~/` or `/` and never go up with `..`. Approving "full access"
is trusting its author, like any extension: that code runs with your
rights.

Your own plugins (a folder you put there, not a git checkout) aren't
checked: they're yours.

## plugin.toml

```toml
[plugin]
id = "theme-foot"          # unique, stable
name = "foot theme"
version = "0.1.0"
api = 1                    # manifest API; the core refuses other values
description = "one line"
requires = ["shell-bar", "hypr-base >= 0.1"]   # other plugins it needs

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
`>`, `=`, `<=`, `<`; `1.2` is `1.2.0`). myarch refuses to apply while an
enabled plugin needs one that is missing, disabled or at a version that
doesn't do; `enable` and `disable` say what else they'd need to take along.
A bar widget requires `shell-bar`; a Hyprland fragment, `hypr-base`.

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
means healthy, and its output is shown when it fails. `myarch doctor` runs
every check; `myarch update` runs them after updating and rolls the update
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
run = "qs ipc -c myarch call myarch panel monitors"   # the command it runs
key = "{{ settings.key }}"                         # its keybinding
terminal = false                                      # run it in a terminal
keywords = "displays screens resolution"              # other words for it
```

`run` is a shell command, and the palette shows it next to the action: the
point is that people learn it. When the plugin's keybinding calls a Lua
function, expose that function as a global and make `run` call it through
`hyprctl eval` (see `columns`: `myarch_columns.equal()`), so the key and
the command do exactly the same thing. An action with a `key` and no `run`
is a keybinding that only makes sense as a key ("SUPER + 1…0"). Every
keybinding a plugin binds should be one of its actions: that's how the
palette's list of keys stays right. `terminal = true` is for commands that
ask or print (`myarch update`); the terminal stays open afterwards.

## Coverage

What a plugin themes, so `myarch coverage` (and the theme picker) can tell
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
`~/.config/myarch/config.toml`:

```toml
[plugins.columns]
auto = true
min_width = 400
```

Overrides must use a key the plugin declares and the same type as the
default (an integer is accepted where the default is a float). Anything
else, including a `[plugins.<id>]` section for a plugin that doesn't exist,
stops `apply` with an error naming the section. `myarch plugins` prints each
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
a key missing everywhere fails the render.

In templates, `t "key"` gives the text and `tq "key"` gives it as a quoted
literal that is valid in QML/JS and Lua. Placeholders are `%1`, `%2`… and
filled in with QML's `.arg()`: `{{tq "updated"}}.arg(time)`. `.Lang`
(`es_MX`) and `.LangCode` (`es`) are there for APIs that take a language,
and for `Qt.locale(…)`, which gives day names and time formats for free.

## Targets

The core renders every target and writes it; plugins never touch the disk.
That gives three guarantees:

- **Ownership.** The core records a hash of each file it writes
  (`~/.local/state/myarch/owned.json`). A file it didn't write, or one a
  person edited afterwards, is a *conflict*: `apply` stops and lists it.
  `apply --adopt` backs it up (`*.myarch-bak-<time>`) and takes it over.
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
  files: taking one of myarch's lines out is an edit (a conflict, not put
  back), and myarch's lines leave with the plugin (or when a newer version
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
whose template loops forever can't hang myarch. A template gets the data
made anew: what it changes (an item of `actions`, say) no other template,
plugin or command sees. A template is a `*.tmpl` file in the plugin's own
folder, never a path out of it. Scriban's functions are all there but those
that read or run something else (`include`, `object.eval`) or change on
every apply (`date.now`, `math.random`).

A text setting that goes into code is always quoted for it (`lq`, `quote`,
`inline`): its value must never be able to close a string and add code.

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
| `lq settings.key`          | any string as a Lua literal: `"SUPER + space"` |
| `inline settings.style`    | without line breaks: for a comment or an ini value |
| `under "~/.config/hypr/myarch/"` | every plugin output below that path |
| `json actions`             | a JSON value, also a valid QML/JS literal |

`under` is how an entry point includes fragments without knowing which
plugins exist: `hypr-base` loads every file other plugins generate under
`~/.config/hypr/myarch/`, each one isolated so a broken fragment can't stop
the rest from loading.

## Theme tokens

The token names in `core/src/MyArch/Themes/Theme.cs` (`RequiredColors`,
`RequiredAnsi`) are part of this API. Ask for meaning (`danger`,
`surface_raised`), not for a hue; `[ansi]` is for terminals and TUIs.

The accent tokens (`accent`, `accent_fg`, `accent_text`, `accent_deep`,
`selection`) may not be the theme's: when the person picks another accent
(`myarch apply --accent`, the theme picker), they're derived from it with
their contrast kept. Rely on what each one is for, not on its value.

The theme picker previews themes live: QML that reads `Theme.<token>` in
bindings morphs with it for free. A color copied into a plain value
(`property string c: "" + Theme.accent`) doesn't follow; Hyprland-side
colors follow through `myarch_theme({...})` (plugin `theme-hyprland`).

A theme's `[effects]` are optional: `terminal_opacity` (0.5–1),
`blur` (see-through windows blur what's behind), and `wallpaper`: `"grid"`
(drawn from the tokens), `"plain"` (bg_alt) or an image next to
theme.toml. Templates read them as `theme.effects.terminal_opacity`,
`.blur`, `.wallpaper`; QML as `Theme.terminalOpacity`, `Theme.blur`,
`Theme.wallpaper`, `Theme.mode`.

A theme can suggest accents besides its own, in `[meta]`:
`accents = ["#d99a2b", "#4fa35f"]`. `myarch themes` lists every theme with
the contrast pairs it fails (WCAG: 4.5:1 for text, 3:1 for fills).

## Bar widgets

The bar (plugin `shell-bar`) has no widgets of its own. A plugin adds one by
generating a QML file into a slot:

```
~/.config/quickshell/myarch/widgets/left/<NN>-<name>.qml
~/.config/quickshell/myarch/widgets/center/<NN>-<name>.qml
~/.config/quickshell/myarch/widgets/right/<NN>-<name>.qml
```

`NN` orders widgets within a slot. Each one loads on its own: one that
fails shows a warning in its place and the rest keep working. The root item
needs an `implicitWidth`; it may declare `property var barScreen` and
`property int barHeight`, set once it loads, and `property bool shown`: a
widget with nothing to show sets it to false and the bar takes it out, gap
included, until it's true again (use this, not `visible`). Shared, non-widget files (a
data service, a component) go under `components/<name>/`.

After writing, a widget plugin asks the running shell to reload with
`reload = "sh ~/.local/share/myarch/bin/shell-reload"` (generated by
`shell-bar`; it also starts the shell if it isn't running).

Panels are windows that aren't part of the bar (the monitor manager, a
launcher): a plugin generates `~/.config/quickshell/myarch/panels/<name>.qml`,
whose root item has `function toggle()`, `property bool open` and
`function close()`, and creates its own windows. The shell loads it once;
`qs ipc -c myarch call myarch panel <name>` opens or closes it (bind that to
a key from the plugin's Hyprland fragment). A panel window that takes the
keyboard uses the layer namespace `myarch-panel-<name>`: then SUPER + Q
closes it, like any window (`qs ipc -c myarch call myarch close` closes
every open panel), instead of closing the window behind it. Panels import
the kit as `"../components/kit"`.

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

They all take their colors, font and shape from the theme.
