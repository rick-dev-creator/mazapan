# Network

The network in the bar: a cable when wired, the Wi-Fi's signal otherwise,
dimmed when there's no connection. A click opens the card: a Wi-Fi
switch, the wired connections (with their speed, or "cable unplugged"),
and the Wi-Fi networks in range, the connected one first, then the saved
ones, then by signal. It looks for networks only while the card is open.

A click on a network joins it, or leaves it if connected; a secured one
you haven't joined before asks for its password right there. A right
click forgets a saved network.

"Share this Wi-Fi" shows a code that a phone's camera scans to join the
network you're on (the password comes from NetworkManager; the code is
made with qrencode, only while it's shown). "Speed test" measures
download, upload and latency against Cloudflare's speed test
(speed.cloudflare.com, no account): about 25 MB down and 10 MB up.

It talks to NetworkManager; "Check that everything works" makes sure it
runs and the computer is on some network.
