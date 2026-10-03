# Bar

A bar at the top of every screen (Quickshell), started with Hyprland.
It has nothing of its own: the clock, the workspaces, the battery and
the rest are widgets other plugins put in its left, center or right.
Each widget loads on its own: one that fails shows a warning in its
place, and the others keep working.

The center stays centered while there's room. On a narrow screen it
moves left, clear of the right, and the left gives up room: first what
can shrink (a window's title), then it's cut.

The panels other plugins open (the Plugins panel, the monitors…) run in
the same shell.

A check makes sure the bar runs and loads its config. After an update
of Quickshell it restarts the bar on the new version first.

`height` (28): the bar's height in logical pixels, 20 to 64.

## For plugin authors

A widget is a QML file in `~/.config/quickshell/mazapan/widgets/left`,
`center` or `right`, placed in file-name order (a prefix like `10-`
picks its place). The bar ships a kit of components for them (a bar
item, its popup card, labels, toggles, sliders, list rows, buttons…) in
`components/kit`. See `docs/plugin-api.md`.
