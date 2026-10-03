# ASUS ROG Flow Z13 touchpad while typing

The ASUS ROG Flow Z13's detachable keyboard has its touchpad seen as an
external one, so libinput doesn't pair it with the keyboard and doesn't
ignore it while you type. Typing fast flexes the keyboard, the touchpad
takes that as taps, and the cursor jumps somewhere else. This marks the
touchpad as built in, and disable-while-typing works.

Offered on the ROG Flow Z13 (GZ302), by the name the firmware gives it.

It writes one udev rule, as root:
`/etc/udev/rules.d/mazapan-asus-z13-touchpad.rules`, for the keyboard's
touchpad (USB `0b05:1a30`). udev takes it at once; the desktop does
when the keyboard is attached again, or at the next login. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-z13-touchpad && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
