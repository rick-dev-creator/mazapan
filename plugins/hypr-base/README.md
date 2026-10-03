# Hyprland base

Where the desktop starts: Hyprland's own config,
`~/.config/hypr/hyprland.lua`. It sets the screens (each at its
preferred mode, placed and scaled automatically), the keyboard, the
mouse and the touchpad, and the keys below, then loads what every other
plugin puts in `~/.config/hypr/mazapan/`. Each of those loads on its
own: a broken one can't keep the rest from loading, and Hyprland shows
its error.

The file is rewritten on every `mazapan apply`: edits to it don't last.
A check makes sure Hyprland accepts it, after each update too.

## Keys

- `SUPER + Return`: a terminal (foot).
- `SUPER + Q`: close the window, or the open panel if there is one.
- `SUPER + V`: float or tile the window.
- `SUPER + J`: swap the split direction.
- `SUPER + ← ↑ → ↓`: focus the window to that side.
- `SUPER + 1…0`: go to workspace 1 to 10.
- `SUPER + SHIFT + 1…0`: move the window to workspace 1 to 10.
- `SUPER + SHIFT + E`: exit Hyprland.

## Settings

- `kb_layout` ("us"): the keyboard layouts, comma separated ("us,es"),
  by xkb's names; `kb_variant` (none) each one's variant, in the same
  order ("intl"); `kb_options` (none) xkb's options, such as a key to
  switch layouts ("grp:alt_shift_toggle"; with several layouts and no
  such key, there's no way to switch).
- Touchpad: `tap_to_click` (on), a tap is a click and two fingers the
  right one; `natural_scroll` (off), the content moves with the
  fingers, as on a phone; `disable_while_typing` (on), so the palm
  doesn't move the pointer.
- `pointer_speed` (0), mouse and touchpad: -1 slowest, 1 fastest.

The keyboard and touchpad are in Settings too (`SUPER + comma`).
