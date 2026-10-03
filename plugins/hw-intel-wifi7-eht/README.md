# Wi-Fi 6 on Intel BE200 and BE211 cards

Intel's BE200 and BE211 Wi-Fi 7 cards (the Dell XPS 14 and 16 on Panther
Lake have one, other laptops too) don't work with Wi-Fi 7 on Linux yet:
the iwlwifi driver's receive path for it is broken, access points drop
to their slowest rate, and Wi-Fi is unusable. This turns Wi-Fi 7 off, so
the card connects with Wi-Fi 6, which works at full speed.

Offered where one of those cards is there (PCI 8086:272b, the BE200, or
8086:e440, the BE211).

It writes one kernel module option, as root: `disable_11be=Y` for
iwlwifi, in `/etc/modprobe.d/mazapan-iwlwifi-eht.conf`. It takes effect
after a reboot. No settings. It's a stopgap: once Intel fixes the
driver, turn it off and Wi-Fi 7 is back.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-intel-wifi7-eht && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` takes it back.
