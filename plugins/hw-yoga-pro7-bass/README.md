# Lenovo Yoga Pro 7 bass speakers

The Lenovo Yoga Pro 7 (14IAH10) has bass speakers that stay silent
without the right pin model for its sound codec. This gives it that
model, and they play.

Offered on that model only (the Yoga Pro 7 14IAH10, by the name the
firmware gives it).

It writes one kernel module option, as root: `hda_model` for the sound
driver (`snd-sof-intel-hda-generic`), in
`/etc/modprobe.d/mazapan-yoga-pro7-bass.conf`. It takes effect after a
reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-yoga-pro7-bass && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` takes it back.
