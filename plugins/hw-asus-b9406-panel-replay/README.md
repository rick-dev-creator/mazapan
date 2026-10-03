# ASUS ExpertBook B9406 screen that keeps updating

On the ASUS ExpertBook B9406 (Intel Panther Lake, Xe3 graphics) the
screen freezes on its last frame and only changes on a full modeset,
such as switching to a text console and back. Panel Replay, new in Xe3
and on by default, never wakes this panel. This turns it off, and the
screen follows what's drawn again.

Offered on the ExpertBook B9406 with Panther Lake graphics.

It writes one kernel module option, as root: the xe driver's
`enable_panel_replay=0`, in
`/etc/modprobe.d/mazapan-asus-b9406-panel-replay.conf` (turning off
PSR alone doesn't cover Panel Replay). The driver starts from the
initramfs, so that's rebuilt, and it takes effect after a reboot. No
settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-b9406-panel-replay && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
