# AMD Strix and Krackan Point laptop screen that keeps updating

On laptops with AMD Ryzen AI 300 processors (Strix Point, Radeon 880M
and 890M graphics; Krackan Point, Radeon 840M and 860M) the screen can
freeze on its last frame, often right as the desktop or the login screen
appears: the cursor stays where it was and the keyboard seems dead,
though the system keeps running. Panel Self Refresh (PSR, and its
selective update, PSR2-SU) never wakes the panel. The ASUS Zenbook S 16
(UM5606) does it on Linux 7. This turns PSR off, and the screen follows
what's drawn again.

Offered on machines with Strix Point or Krackan Point graphics. The
installer turns it on there by itself; every entry of the ISO's boot menu
carries the same option, for the live session.

It writes one kernel module option, as root: the amdgpu driver's
`dcdebugmask=0x600`, in `/etc/modprobe.d/mazapan-amd-strix-psr.conf`
(`0x200` alone, PSR-SU only, isn't enough on the Zenbook S 16). The
driver starts from the initramfs, so that's rebuilt, and it takes effect
after a reboot. The screen draws a little more power with PSR off. No
settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-amd-strix-psr && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` takes it back.
