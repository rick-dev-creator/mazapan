# Firewall

A firewall that's simply on, as on macOS: nothing comes in that this
computer didn't ask for, and everything it asks for goes out. Printers
and other devices on the network are still found (ufw's own rules let
mDNS and SSDP in). One port is open, to the local network only (never the internet):
LocalSend's (53317), so phones and computers nearby can send to this one, as on Omarchy ("Let LocalSend in";
nothing listens there unless LocalSend runs). SSH's isn't, unless "Let SSH
in" is on (the installer turns it on when it was given SSH keys).

The installer turns it on. Taking the plugin off turns the firewall off.
What it does is ufw's: `sudo ufw status verbose` shows it.

Docker publishes its containers' ports past any firewall (it writes its
own rules): with Docker, mind what you publish.
