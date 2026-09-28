# Plugin API v1

Everything myarch does is a plugin, built-ins included: they use exactly
the API described here. If something can't be expressed with it, the API
grows; built-ins never get a private path.

## Where plugins live

```
~/.local/share/myarch/plugins/<dir>/plugin.toml   your plugins (searched first)
<repo>/plugins/<dir>/plugin.toml                  built-ins
```

A plugin id found in the user directory shadows the built-in with the same
id. Disable a plugin in `~/.config/myarch/config.toml`:

```toml
theme = "phosphor"
disabled_plugins = ["theme-foot"]
```

## plugin.toml

```toml
[plugin]
id = "theme-foot"          # unique, stable
name = "foot theme"
version = "0.1.0"
api = 1                    # manifest API; the core refuses other values
description = "one line"

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

Unknown keys are an error, so typos don't pass silently.

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

After writing, each distinct `reload` command of plugins whose files changed
runs once. A failing reload is a warning: the files are already in place.

## Templates

Go `text/template`. All `*.tmpl` files of a plugin are parsed together, so
`{{define}}` blocks are shared between its targets (see `theme-gtk`). A
missing token fails the render instead of producing an empty value.

`.` is:

| Field          | Meaning                                         |
|----------------|-------------------------------------------------|
| `.Theme`       | the theme: `.ID`, `.Meta`, `.Font`, `.Shape`, `.Motion`, `.Colors`, `.ANSI` |
| `.Plugin`      | this plugin's id                                |
| `.Home`        | the user's home directory                       |
| `.Settings`    | the plugin's settings, defaults merged with config.toml |

Functions:

| Function                   | Example result                |
|----------------------------|-------------------------------|
| `c "accent"`               | `#ffb000` (colors, then ansi) |
| `hex (c "accent")`         | `ffb000`                      |
| `rgb (c "accent")`         | `rgb(ffb000)` (Hyprland)      |
| `rgba (c "accent") 0.5`    | `rgba(ffb00080)` (Hyprland)   |
| `cssa (c "accent") 0.36`   | `rgba(255, 176, 0, 0.36)`     |
| `speed 0.6`                | Hyprland speed for 60% of the theme duration |
| `num 11.0`                 | `11`                          |
| `under "~/.config/hypr/myarch/"` | every plugin output below that path |

`under` is how an entry point includes fragments without knowing which
plugins exist: `hypr-base` loads every file other plugins generate under
`~/.config/hypr/myarch/`, each one isolated so a broken fragment can't stop
the rest from loading.

## Theme tokens

The token names in `core/internal/theme/theme.go` (`RequiredColors`,
`RequiredANSI`) are part of this API. Ask for meaning (`danger`,
`surface_raised`), not for a hue; `[ansi]` is for terminals and TUIs.
