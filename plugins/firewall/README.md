# Firewall

A firewall that's simply on, as on macOS: nothing comes in that this
computer didn't ask for, and everything it asks for goes out. Printers
and other devices on the network are still found (ufw's own rules let
mDNS and SSDP in). No port is open, SSH's neither, unless "Let SSH in"
is on (the installer turns it on when it was given SSH keys).

The installer turns it on. Taking the plugin off turns the firewall off.
What it does is ufw's: `sudo ufw status verbose` shows it.

Docker publishes its containers' ports past any firewall (it writes its
own rules): with Docker, mind what you publish.
