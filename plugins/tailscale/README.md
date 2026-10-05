# Tailscale

Tailscale ready to use:

- its service, `tailscaled`, started now and at every start;
- you made its operator, so signing in (`tailscale up`) and sending or
  receiving files with Taildrop need no sudo (kept so every time the
  service starts);
- "Tailscale: sign in" in the palette prints the address to sign in at;
  "Tailscale: devices and status" lists your tailnet;
- files your other devices send you with Taildrop land in Downloads (a
  name already there: renamed; received first into `Downloads/.taildrop`,
  so only they are announced), each with a notification to open it or
  its folder (`receive`).

Sending is the Share plugin's: your devices that are online show in its
panel.

What it does, as root: installs `tailscale`, and writes
`/etc/systemd/system/tailscaled.service.d/mazapan.conf` (the operator, set
once the service is up), enabling and restarting `tailscaled`. Turned off,
the service is stopped and not started any more. As you: the receiving
service (`~/.config/systemd/user/mazapan-taildrop.service`) and its
script.

It's off until turned on: installing Tailscale in Apps turns it on, or
`mazapan plugins enable tailscale && mazapan apply --system`.
