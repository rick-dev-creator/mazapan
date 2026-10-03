# Wi-Fi passwords on Macs with Broadcom Wi-Fi

On Macs whose Broadcom Wi-Fi runs on the brcmfmac driver, the card's
firmware does the WPA handshake itself, and against an access point in
WPA2/WPA3 transition mode it never completes: the Mac joins, then
NetworkManager says the password is wrong. This hands the handshake
back to wpa_supplicant, and the right password works.

Offered on Macs with one of the Wi-Fi chips brcmfmac drives: the
BCM43602 of 2015-2017, the BCM4350, BCM4355 and BCM4364 of 2018-2019,
and the BCM4377, BCM4378 and BCM4387 from the T2 era on (every T2 Mac
too). The BCM4360 of 2013-2015 isn't one: the wl driver runs it
(`hw-broadcom-wl`).

It writes one kernel module option, as root: brcmfmac's
`feature_disable=0x82000` (the firmware's supplicant and authenticator
off), in `/etc/modprobe.d/mazapan-apple-brcmfmac.conf`. It takes effect
after a reboot. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-apple-brcmfmac-wpa && mazapan apply
--system`. What runs as root is listed first, and asks for your password
in a terminal; `mazapan undo` takes it back.
