# Cursor on the nouveau driver

The mouse cursor drawn in software where an NVIDIA GPU runs nouveau, the
open driver that comes with the kernel. On many older NVIDIA GPUs
nouveau doesn't show the hardware cursor, so under Hyprland the pointer
is invisible. Drawn in software, the cursor is part of the frame
instead.

Offered where the nouveau driver is loaded. Where NVIDIA's own driver is
set up instead (`hw-nvidia` on: during an install, nouveau still runs
until the first reboot), it does nothing, since that driver shows the
hardware cursor; the same once no GPU runs on nouveau.

Nothing runs as root: it's one line of Hyprland's config,
`~/.config/hypr/mazapan/hw-nouveau-cursor.lua`, taken at once. No
settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-nouveau-cursor && mazapan apply`.
