# MacBook NVMe waking from sleep

The NVMe drive of the 2015-2017 MacBook and MacBook Pro fails to wake
from its deepest power state (D3cold): after sleep the system can't
reach its disk. This keeps the drive out of that state; it still sleeps,
just not that deeply.

Offered on those models, by the name the firmware gives them:
MacBook8,1, MacBook9,1, MacBook10,1, MacBookPro13,1–3 and
MacBookPro14,1–3.

It writes one file, as root: `/etc/tmpfiles.d/mazapan-apple-nvme-suspend.conf`,
which sets `d3cold_allowed` to 0 for the drive (at PCI address
`0000:01:00.0` on these models) at every start. It's set at once too,
without a reboot; turned off, the drive is allowed into D3cold again.
No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-apple-nvme-suspend && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
