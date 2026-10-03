# Intel low power mode

Intel's Low Power Mode Daemon (`intel_lpmd`), for laptops with a hybrid
Intel processor: Alder Lake, Raptor Lake, Meteor Lake, Lunar Lake and
Panther Lake. When the computer is doing little, it keeps that work on
the efficient cores and lets the others rest, so the battery lasts
longer.

Offered on laptops with one of those processors, told by their built-in
graphics (Alder Lake-P, -U and -HX, Raptor Lake-P and -U, Meteor Lake,
Lunar Lake, Panther Lake). Not on desktops, nor on Alder Lake-N, which
isn't hybrid.

What it does, as root: installs `intel-lpmd`, and starts
`intel_lpmd.service` now and at every start. The file that says it's on
is `/etc/tmpfiles.d/mazapan-intel-lpmd.conf`; its own settings are in
`/etc/intel_lpmd/`. Turned off, the service is stopped and not started
any more. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-intel-lpmd && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` takes it back.
