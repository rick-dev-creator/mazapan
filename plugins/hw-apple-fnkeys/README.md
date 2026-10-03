# Function keys first on Apple keyboards

F1–F12 as function keys, and the media keys with Fn, on the keyboards
the hid_apple driver runs: Apple's, and others in Mac mode (Keychron…).
Left alone, the kernel guesses which comes first.

Offered where the hid_apple driver is loaded. Once on, it stays on with
the keyboard unplugged.

It writes one kernel module option, as root: hid_apple's `fnmode`, in
`/etc/modprobe.d/mazapan-hid-apple.conf`, for every boot. The driver
already loaded takes it at once, without a reboot.

`fnmode` (2): 2, function keys first; 1, media keys first; 3, the
kernel's guess (its default); 0, no Fn key.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-apple-fnkeys && mazapan apply --system`.
What runs as root is listed first, and asks for your password in a
terminal; `mazapan undo` takes it back.
