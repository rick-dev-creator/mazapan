# Framework Laptop 16 keyboard lighting

The Framework Laptop 16's keyboard takes its RGB lighting and key
settings through raw HID, which only root can reach by default. This
gives it to you while you're logged in at the machine, so `qmk_hid` or
the VIA configurator in a browser can talk to it without sudo.

Offered on the Framework Laptop 16.

It writes one udev rule, as root:
`/etc/udev/rules.d/mazapan-framework16-keyboard.rules`, for the
keyboard's raw HID (USB `32ac:0012`), taken at once. `qmk_hid` itself
isn't in Arch's repositories (it's in the AUR), so it isn't installed.
No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-framework16-keyboard && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
