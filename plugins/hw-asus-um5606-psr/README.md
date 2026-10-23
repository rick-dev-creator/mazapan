# ASUS Zenbook S 16 screen that keeps updating

On the ASUS Zenbook S 16 (UM5606, AMD Ryzen AI with Radeon graphics)
the screen freezes on its last frame, often right as the desktop or the
login screen appears: the cursor stays where it was and the keyboard
seems dead, though the system keeps running. Panel Self Refresh (PSR,
and its selective update, PSR2-SU) never wakes this OLED panel. This
turns it off, and the screen follows what's drawn again.

Offered on the Zenbook S 16 (UM5606) with AMD graphics. The installer
turns it on there by itself; every entry of the ISO's boot menu carries
the same option, for the live session.

It writes one kernel module option, as root: the amdgpu driver's
`dcdebugmask=0x600`, in `/etc/modprobe.d/mazapan-asus-um5606-psr.conf`
(`0x200` alone, PSR-SU only, isn't enough on this panel). The driver
starts from the initramfs, so that's rebuilt, and it takes effect after
a reboot. The screen draws a little more power with PSR off. No
settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-um5606-psr && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
