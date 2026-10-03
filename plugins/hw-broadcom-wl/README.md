# Broadcom BCM4360 and BCM4331 Wi-Fi

The Broadcom BCM4360 (2013-2015 MacBooks) and BCM4331 (2012 and early
2013 MacBooks), also found in other laptops, get little or no Wi-Fi from
the kernel's own drivers. Broadcom's wl driver runs them.

Offered where one of those two chips is there (PCI `14e4:43a0` or
`14e4:4331`). The newer Broadcom chips in Macs run on brcmfmac instead
(see `hw-apple-brcmfmac-wpa`).

It writes no files: it installs `broadcom-wl-dkms` and `linux-headers`,
as root. The driver is built for your kernel on install, so every
installed kernel needs its headers; a check looks for them first. The
package keeps the kernel's own Broadcom drivers from loading, and the
Wi-Fi comes up on wl after a reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-broadcom-wl && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` uninstalls them, unless something else needs
them by then.
