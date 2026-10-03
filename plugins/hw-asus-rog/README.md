# ASUS ROG laptop controls

asusctl, for ASUS ROG laptops: performance profiles, fan curves, a
limit on how far the battery charges, and the keyboard's lighting,
through `asusctl` and its daemon, asusd.

Offered on ASUS laptops with ROG in their model name (most of them;
an older ROG whose name doesn't say so isn't offered it).

It writes no files: only `asusctl`, from Arch's repositories, installed
as root. asusd is started by its own udev rule on ASUS machines, so it
runs from the next boot; a check says whether it does. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-asus-rog && mazapan apply --system`. What
runs as root is listed first, and asks for your password in a terminal;
`mazapan undo` uninstalls it, unless something else needs it by then.
