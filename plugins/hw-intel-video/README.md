# Video decoding on Intel graphics

Video decoded by the graphics hardware (VA-API) on Intel graphics. It
installs both of Intel's drivers, `intel-media-driver` (iHD, Broadwell
and newer) and `libva-intel-driver` (i965, older), and libva picks the
one for your GPU.

Offered on Intel graphics. It writes no files: only the two packages,
installed as root. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-intel-video && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` uninstalls them, unless something else needs
them by then.
