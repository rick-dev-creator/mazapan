# Framework Laptop 13 (AMD) microphones

On the Framework Laptop 13 with AMD Ryzen the sound card can start on a
profile without the built-in microphones, and nothing records. This
puts it on the one that has them, "HiFi (Mic1, Mic2, Speaker)".

Offered on the Framework Laptop 13 with AMD graphics.

Nothing it writes needs root: one WirePlumber file,
`~/.config/wireplumber/wireplumber.conf.d/mazapan-framework13-amd-mic.conf`,
that prefers that profile for the analog card (AMD's "Family 17h/19h"
HD Audio) whenever WirePlumber has none stored for it. It's set at once
too, which stores it. Turned off, the card stays on that profile until
you pick another in the sound settings. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-framework13-amd-mic && mazapan apply`.
