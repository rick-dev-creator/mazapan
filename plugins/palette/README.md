# Command palette

One place for everything: apps, open windows, and every plugin's actions
and keybindings. `SUPER + Space`, or the button at the start of the bar
(`bar`, on).

## The first screen

Opened with nothing typed, it's the desktop's menu: a tile for each place
(Apps, Updates, Settings, Screens, Capture…; Updates says how many are
waiting), the computer's power (a second ↵ confirms power off, reboot and
the like), and the open windows below. The arrow keys move across them.

## Finding

Type to find apps, windows (by title, app or workspace), actions and
keybindings. Letters needn't be together: "vsc" finds Visual Studio Code.
Apps that aren't installed yet are found too, as "Install …", which opens
them in the Apps panel. Start with `> ` to find only actions and keys; with `? ` to ask a coding
agent (with the Agents plugin: the answer comes in a card).

↑ ↓ (or Tab) pick, ↵ runs, Ctrl+C copies the command, Esc closes. A
keybinding that's only a key says which keys to press.

Every result shows the command it runs (`show_commands`, on): you start
by clicking and end up knowing the command.

## Its own actions

"Update the system", "Check that everything works", "Update history",
"Undo the last update" and "Re-apply the configuration": `mazapan update`,
`doctor`, `history`, `rollback` and `apply`, each in a terminal that stays
open to read (`terminal_hold`, `foot --hold`). Terminal apps such as btop
open in `terminal` (`foot`).
