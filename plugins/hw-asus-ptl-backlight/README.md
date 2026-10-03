# ASUS Panther Lake screen brightness

On the ASUS ExpertBook B9406 and Zenbook UX5406AA (Intel Panther Lake)
the brightness keys and slider do nothing in between: the screen is
either full or off. The panel's EDID reads as empty, so the xe driver
drives the backlight the way the firmware's table says (PWM), while the
panel wants it through DisplayPort AUX (DPCD). This tells xe to use
DPCD, and brightness changes in steps again.

Offered on those two models with Panther Lake graphics. Other ASUS
Panther Lake laptops may have the same problem; they aren't offered it
until that's known.

It writes one kernel module option, as root: the xe driver's
`enable_dpcd_backlight=1`, in
`/etc/modprobe.d/mazapan-asus-ptl-backlight.conf`. The driver starts
from the initramfs, so that's rebuilt, and it takes effect after a
reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-ptl-backlight && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
