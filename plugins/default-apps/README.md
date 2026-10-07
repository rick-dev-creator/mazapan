# Default apps

What opens what: links, PDFs, pictures, videos, music, text, folders,
mail links, office documents and archives; and the terminal. Where there's no default (or
it's an app that's gone), the first installed of the usual ones; a
default chosen in an app ("make Firefox my browser") stays. Set one in
its settings (a desktop file's name, "firefox.desktop") and it's that
one. Run on every apply and after the Apps menu installs or removes an
app.

**One key to each**: `SUPER + B` goes to the browser's window when it's
open (the one used last), and opens it when it isn't; `SUPER + E` the same
for the files (`browser_key`, `files_key`; empty: no key).

**Choosing**: Settings, Default apps: each kind offers the usual apps
installed, then any other installed app that says it opens that kind (its
desktop file's MimeType, as `gio mime` lists them); the terminal, every
installed terminal. In Apps, an installed app that can be the default for
something it isn't yet has "Use as default …" beside it (its main kind:
a terminal, a browser…).

**The terminal**: SUPER + Enter, the palette's terminal apps, and what
opens a terminal to ask or show (Plugins, Updates, a crash) all open the
default one, through `xdg-terminal-exec`: the first installed in
`~/.config/xdg-terminals.list`. The one chosen goes first; others there
stay below it. Unchosen, the first installed of foot, Ghostty, kitty,
Alacritty, WezTerm, Ptyxis, Console, Konsole. `xdg-terminal-exec
--print-id` says which it is.

From a script: `default-apps --kinds ID` says what an app (a desktop
file's name) can be the default for; `default-apps --registered TYPE…` the
installed apps that open each type.
