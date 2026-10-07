# Bluetooth

Bluetooth in the bar: its icon says whether it's off, on, or connected to
something (and to how many, when more than one). A click opens the card: a
switch for Bluetooth itself, then your devices, connected ones first, then
paired ones, each with its battery when it reports one.

To add a device, turn on "look for new devices": new ones show up while
the card is open, and it stops looking when the card closes. A click on a
new one pairs it, trusts it (so it reconnects on its own) and connects it.
A click on a paired device connects or disconnects it; a right click
forgets it.

Without a Bluetooth adapter there's nothing to do, and the icon hides
(`hide_without_adapter`, on). It talks to BlueZ; if you use Bluetooth
(an adapter, and its service enabled), "Check that everything works" makes
sure it's running.

With Share on, a phone or computer you paired has a send button at its
right (here and in the Control Center): choose files, and they go to it,
their progress in the Share panel. A pairing that asks to confirm a code
(a phone) is confirmed in the Share panel, which opens with the code.
