# Columns

Windows side by side as columns on a strip that scrolls, as
[Niri](https://github.com/YaLTeR/niri) does it, on Hyprland's scrolling
layout:

- A new window opens to the right at half the screen (`width`); the
  others keep their width, and the strip scrolls to show the one in focus.
- `SUPER + R`: the focused column a third, half, two thirds, all of the
  screen, in turn (`SUPER + SHIFT + R` the other way round). `SUPER + F`:
  all of the width, and back. `SUPER + SHIFT + F`: fullscreen.
- `SUPER + [` / `SUPER + ]`: the window into the column on its left or
  right (two windows stacked in one column), or, in a column with others,
  out into its own.
- `SUPER + Page Down` / `SUPER + Page Up`: the next or previous workspace
  on this screen, a new empty one after the last (with `SHIFT`, the window
  goes along).
- `SUPER` + the wheel scrolls the strip; with `SHIFT`, workspaces. On a
  touchpad, three fingers sideways scroll the strip, up and down change
  workspace.
- In the bar, next to the workspaces: a block per column, as wide as it
  is, the ones on the screen filled and the focused one in the accent; a
  click goes there. `SUPER + Tab` (the Workspaces plugin) shows every
  workspace with its whole strip.

On top of that, three arrangements of the old kind, remembered for each
workspace:

Windows side by side as columns (Hyprland's scrolling layout), instead of
each new window halving the one before. Three arrangements, remembered
for each workspace:

- **Equal** (`SUPER + =`): every column the same width, filling the
  screen. With more windows than fit at `min_width` (400 logical pixels),
  the columns keep that width and the row scrolls.
- **Phone** (`SUPER + SHIFT + =`): every column a phone's width, the
  group centered on the screen. Again, and it's back to equal.
  `phone_aspect` is a column's width to height (0.4615, a phone held
  upright), so the width follows each screen's height.
- **Focus** (`SUPER + C`): the focused window in the middle, at
  `focus_ratio` of the width (55%), the others stacked in a column on
  each side. On a side window, it brings that one to the middle; on the
  middle one, it leaves focus mode.

On the focused column, `SUPER + ALT + →` and `SUPER + ALT + ←` make it
wider or narrower (in focus mode, the middle one) by `resize_step` (5% of
the screen), never below `min_width`; held down, they repeat.
`SUPER + SHIFT + ←` and `SUPER + SHIFT + →` move it one place along the
row. Every key can be changed, and they're all in the palette too.

With `auto` on, the columns are arranged again whenever a window opens,
closes or moves in, so they always share the screen (equal widths, as
before Niri's way was the default).
