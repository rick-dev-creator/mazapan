# MacBook SPI keyboard at boot

The 2015-2017 MacBook and MacBook Pro have their keyboard and touchpad
on SPI, which the kernel's applespi driver runs. Left to load on its
own it comes up once the system has started: too late for the disk
password, asked for before that. This loads it from the start.

Offered on those models, by the name the firmware gives them:
MacBook8,1, MacBook9,1, MacBook10,1, MacBook12,1, MacBookPro13,1–3 and
MacBookPro14,1–3.

It writes one file, as root: applespi and its SPI controller in the
initramfs, in `/etc/mkinitcpio.conf.d/mazapan-apple-spi-keyboard.conf`
(the MacBook8,1's controller is a PCI device; later models reach it
through Intel's LPSS). A check makes sure the kernel has those modules
first. The initramfs is rebuilt, and it takes effect after a reboot.
No settings.

The Touch Bar isn't covered: that needs a driver from outside the
kernel, in the AUR.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-apple-spi-keyboard && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
