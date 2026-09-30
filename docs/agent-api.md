# Mazapán for agents

An agent (Claude Code, Codex, any MCP client) can read this desktop's state
and change it the way a person would: through Mazapán, previewed with the
exact diff, and undoable. Never by editing generated files: Mazapán would take
the edit for a conflict and stop managing the file.

## The MCP server

```sh
claude mcp add --scope user mazapan -- mazapan mcp     # Claude Code; others alike
```

`mazapan mcp` speaks the Model Context Protocol on stdin/stdout (JSON-RPC, one
message per line; protocol 2025-06-18, 2025-03-26 and 2024-11-05). Each tool
runs the `mazapan` command it's named after, so an agent can do exactly what
the CLI does, and nothing else:

| Tool             | Does                                                        | Changes anything |
|------------------|-------------------------------------------------------------|------------------|
| `status`         | the whole state as JSON (below); `checks: true` runs the checks too | no |
| `doctor`         | every plugin's health checks, as JSON                       | no |
| `themes`         | every theme, its colors, suggested accents, contrast problems | no |
| `plugins`        | every plugin; with `id`, what it needs and does             | no |
| `coverage`       | installed apps and whether the theme reaches them           | no |
| `history`        | past updates, and the applies that can be undone            | no |
| `preview_change` | what a change would do: the plan and a unified diff of every file | no |
| `apply_change`   | apply a change and write the files; says its undo id        | yes, undoable |
| `undo`           | put back what the last apply changed; with `id`, only if that one is still the last | yes |

A change is `{ "theme": "paper", "accent": "#4fa35f", "set": {"bar-clock.font_size": 11},
"reset": ["bar-clock.format"], "enable": ["agent"], "disable": ["bar-weather"] }`,
any of them. Settings are checked against the plugin's, enabled or not (a
wrong key or type is an error, never a silent no-op); requirements between
plugins too; a theme is one of the themes, never a path.

What an agent can't do, on purpose:
- set a text setting: text can be a command (`palette.terminal`), a key
  binding, anything a template puts in code. It gets the exact command for
  the person instead (`mazapan apply --set palette.key="SUPER + space"`).
  Numbers and switches are its to change.
- install, update or remove plugins from git (their capabilities are the
  person's to approve), or update the system (`mazapan update` asks, runs
  sudo, and can roll back).
- anything as root: turn hardware plugins on or off or change them, apply
  with `--system`, or undo what was done as root (`status --json` marks
  those undo entries `as_root`).

`apply_change` and `undo` are marked destructive, so clients ask before
running them. An agent undoing its own change passes the id `apply_change`
gave: if the person applied something since, undo refuses rather than undo
theirs.

## The CLI underneath

```sh
mazapan status [--json] [--checks]
mazapan doctor [--json]
mazapan apply --dry-run --diff [--theme ID] [--accent #RRGGBB] [--set PLUGIN.KEY=VALUE]…
             [--reset PLUGIN.KEY]… [--enable ID]… [--disable ID]…
mazapan apply …                   # the same, written; config.toml saved with it
mazapan undo [--list] [-y]
mazapan report                    # what's wrong, as Markdown
```

`--set` takes a TOML value (`true`, `480`, `0.5`, `"text"`, `["a", "b"]`);
anything that isn't one is taken as text.

Every apply is undoable: before writing, Mazapán keeps what it will touch
(each file as it was, or that it wasn't there, owned.json, config.toml) in
`~/.local/state/mazapan/applies/` (private: 0600 copies in 0700 folders);
`mazapan undo` puts the latest back. A file changed since (edited by hand, or
written by a later apply) is left as it is: undo never loses what came after.
An apply that changed nothing leaves nothing to undo; one killed half way
can still be undone. The last 20 are kept. One apply or undo runs at a time
(the theme picker's, the palette's and an agent's wait for each other).

In templates, a text setting that goes into code is quoted for it: `lq` for
Lua, `quote` for QML/JS, `inline` (no line breaks) for comments and ini
values; numbers and switches are checked against their default's type.

## `status --json`, version 1

```json
{
  "version": 1,
  "theme": {"id": "gruvbox", "name": "Gruvbox", "mode": "dark", "accent": "#4fa35f", "custom_accent": "#4fa35f"},
  "language": "en_US",
  "plugins": [{"id": "bar-clock", "name": "Clock", "version": "0.1.0", "enabled": true,
               "origin": "built-in", "description": "…"}],
  "broken": [{"id": "…", "error": "…"}],
  "files": {"total": 65, "unchanged": 64,
            "changes": [{"path": "/home/…/foot.ini", "plugin": "theme-foot", "state": "conflict"}],
            "orphans": []},
  "updates": {"last": {"id": "20260928-130529", "outcome": "ok", "finished": "…",
                       "packages": 2, "failed_checks": 0, "note": ""},
              "live": ["20260928-130529"]},
  "undo": [{"id": "20260928-230400-298", "time": "…", "what": "theme amber", "files": 16}],
  "checks": [{"plugin": "…", "name": "…", "ok": true, "skipped": false, "output": "", "took_ms": 9}],
  "problems": []
}
```

- `origin`: `built-in`, `local` (a folder you put in the plugin folder) or
  `git` (then `source` and `commit` too).
- `files.changes[].state`: `new`, `changed` (Mazapán would rewrite it),
  `conflict` (someone edited it: apply stops unless `--adopt`), `busy` (its
  app is running), `unreadable` (not plain JSON: left alone).
- `checks` only with `--checks` (they run commands). `theme` and `files` are
  null when the configuration doesn't load; `problems` says why. The exit
  status is 1 when there are problems.

`doctor --json` is `{"version": 1, "session": true, "checks": […], "failed": 0}`,
exit status 1 when a check fails.

Fields are only ever added within a version; a change that would break a
reader bumps it.

## Asking an agent from the desktop

The `agent` plugin adds to the palette "Ask an agent about this desktop": it
opens the agent (setting `command`, `claude` by default) in a terminal with
`mazapan report` as its first message: the state, failing checks with their
output, generated files that differ, the last update, crashes in the last
day (coredumpctl), and the errors Hyprland and the shell logged, repeated
lines counted once. "Copy a report of what's wrong" puts it on the
clipboard, for pasting anywhere.
