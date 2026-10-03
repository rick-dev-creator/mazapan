# Equal columns

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

The columns are arranged again whenever a window opens, closes or moves
in, so they always share the screen (`auto`, on). Turn it off to keep
columns you resized by hand: then only the keys rearrange them.
