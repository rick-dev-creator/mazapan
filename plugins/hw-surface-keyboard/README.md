# Surface keyboard at boot

The keyboard of Microsoft Surface laptops reaches the system through
the Surface Aggregator, a controller on a serial line behind Intel's
LPSS. Left to load on their own, its modules come up once the system
has started: too late for the disk password, asked for before that.
This loads them from the start.

Offered on Microsoft Surface devices. Tested by Omarchy, where this
comes from, on the Surface Laptop 3; other models may need more.

It writes one file, as root:
`/etc/mkinitcpio.conf.d/mazapan-surface-keyboard.conf`, with the
Surface Aggregator, its HID and keyboard modules, the LPSS serial
modules, and the GPIO pin controller (which differs between Surface
generations: the one loaded when the initramfs is built). A check makes
sure the kernel has those modules first. The initramfs is rebuilt, and
it takes effect after a reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-surface-keyboard && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
