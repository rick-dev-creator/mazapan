# Monitors

Arrange your screens once, and they're arranged that way every time the
same screens are connected again.

## The manager

`SUPER + SHIFT + M`, "Screens" on the palette's first screen, or the
icon in the bar (`bar`, on; with more than one screen it says how many).
Your screens show as live pictures you drag into place: they snap to each
other and never overlap. While it's open, every screen shows its name, so
you know which is which. Click one to set it up: on or off, mode,
scale, rotation, adaptive sync (VRR), 10-bit color, a mirror of another
screen, and which workspaces live on it (`1-5` or `7,8`).

**Try** applies the layout and goes back on its own after 15 seconds
unless you **Keep** it, so a mode that leaves you with a black screen
can't strand you. A screen that can't do adaptive sync or 10-bit color is
left without it, and the manager says so. At least one screen has to stay
on and not be a mirror.

## Profiles

**Save profile** keeps the layout under a name, for exactly these
screens. From then on it's applied on its own at startup and whenever
screens come or go. Screens are recognized by their EDID (make, model,
serial), not by the port they're plugged into, which can change; two
identical ones are told apart by their port.

When the connected screens match no profile, a notification says how to
arrange them (`notify_unknown`, on), and whatever a profile had turned off
comes back on: undocking never leaves you without a screen.

Profiles are kept in `~/.config/mazapan/monitors.json`; the manager writes
it, and you can edit it too.
