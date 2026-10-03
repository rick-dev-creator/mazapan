# Surface Wi-Fi firmware

Many Microsoft Surface devices have Marvell Wi-Fi and Bluetooth, whose
firmware Arch keeps in a package of its own, `linux-firmware-marvell`,
that `linux-firmware` doesn't bring along. Without it there's no Wi-Fi.

Offered on Microsoft Surface devices (on one with another maker's
Wi-Fi it's harmless: only firmware files).

It writes no files: only `linux-firmware-marvell`, installed as root.
The Wi-Fi comes up after a reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-surface-wifi && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` uninstalls it, unless something else needs it
by then.
