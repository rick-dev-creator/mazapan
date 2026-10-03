# Synaptics touchpads over SMBus

Synaptics touchpads through InterTouch (SMBus) instead of PS/2: smooth
scrolling and gestures, on ThinkPads and other laptops.

Offered where the touchpad shows up as "SynPS/2 Synaptics TouchPad",
that is, still on PS/2 (one already on SMBus has another name).

It writes one kernel module option, as root: `synaptics_intertouch=1`
for psmouse, in `/etc/modprobe.d/mazapan-psmouse.conf`. It takes effect
after a reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-synaptics-intertouch && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
