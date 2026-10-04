# Control Center

One panel for what used to be a row of icons on the bar's right, grown out
of a **status pill**:

- **The pill** shows the network, the volume, the battery and how many
  notifications while nothing needs you; when something does, it says what,
  in its color: "Claude · shop-api waits", "Recording 0:42", "Update ready".
- **Now**: only what needs you, each with its action right there (go to the
  agent's window, stop the recording, update).
- **Switches**, two a row: Wi-Fi (its networks open in place), Bluetooth (its
  devices), Do Not Disturb, night light, keep awake, power saver.
- **Sections**: the sound and where it goes (speakers, headphones, HDMI), the
  music playing, your agents and their limits, notifications.

`SUPER + A` opens it, or a click on the pill; Escape or a click outside
closes it. It follows the theme, as everything does.

Every part comes from the plugin it belongs to, as the bar's widgets do:
a plugin puts `control/now/`, `control/tiles/` or `control/sections/`
files, and its bar widget of the same name leaves the bar while the Control
Center is on. Turn the Control Center off, and the bar is as it was.
