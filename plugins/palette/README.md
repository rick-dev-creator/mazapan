# Command palette

One place for everything: apps, open windows, and every plugin's actions
and keybindings. `SUPER + Space`, or the button at the start of the bar
(`bar`, on).

## The first screen

Opened with nothing typed: your apps first (the ones you open most, with
their icons), the open windows, a tile for each place (Apps, Updates,
Settings, Screens, Capture…; Updates says how many are waiting) and the
computer's power (a second ↵ confirms power off, reboot and the like). The
arrow keys move across them.

## Finding

Type to find everything, in sections: the best match, Apps, Windows,
Actions, Settings (a Settings page, a switch such as Wi-Fi or night
light) and Not installed (the catalog's apps, which open in the Apps
panel to install). Letters needn't be together: "vsc" finds Visual Studio
Code. The filters above the list narrow it (a click, or Ctrl ←→); the
Apps filter with nothing typed shows every app. `> ` is the Actions
filter; `? ` asks Mazapan's agent (with the Agents plugin: the mazapán,
in its card), and so does a question as Spanish writes it, `¿…?`.

Every row says what ↵ does to it: Open, Switch to, Run, Change,
Install…. Beside the list, the chosen one: installed or not, where it
opens (its own window, or a terminal), the command it runs
(`show_commands`, on), and buttons for the same. ↑ ↓ pick, Tab goes to the
next section, ↵ runs, Ctrl+C copies the command, Esc closes.

An app that doesn't open says why, in a notification: its program isn't
installed, the terminal it opens in isn't, or it quit at once with an
error.

## Its own actions

"Update the system", "Check that everything works", "Update history",
"Undo the last update" and "Re-apply the configuration": `mazapan update`,
`doctor`, `history`, `rollback` and `apply`, each in a terminal that stays
open to read (`terminal_hold`, `foot --hold`). Terminal apps such as btop
open in `terminal` (`foot`).
