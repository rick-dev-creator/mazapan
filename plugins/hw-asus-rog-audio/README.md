# ASUS ROG laptop speaker volume

On ASUS ROG laptops the Realtek codec's hardware mixer has quirks: the
speakers can sound muffled, and the volume doesn't behave. This sets the
volume in software instead (WirePlumber's soft mixer), for every sound
card, and leaves the hardware mixer alone.

Offered on ASUS laptops with ROG in their model name.

Nothing it writes needs root: one WirePlumber file,
`~/.config/wireplumber/wireplumber.conf.d/mazapan-asus-rog-soft-mixer.conf`.
When it changes, WirePlumber's stored routes are dropped (they hold
volumes for the hardware mixer) and WirePlumber restarts. On an ALC285
codec, its Master control, often muted to begin with and no longer
touched by the volume, is unmuted at 80%. It installs `alsa-utils` (for
that, and to keep the mixer's state across reboots). No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-rog-audio && mazapan apply --system`
(as root only to install `alsa-utils`). `mazapan undo` takes it back.
