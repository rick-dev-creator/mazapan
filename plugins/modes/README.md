# Modes

The whole desktop changed at once, as macOS's Focus does for
notifications (`SUPER + ALT + M`, or "Modes" in the palette). A mode can:
- be quiet (Do Not Disturb) and still let some apps through (Slack, yes;
  the rest, no);
- switch the theme (and with it the wallpaper, if it's the theme's);
- turn night light on or off, and pick the power profile;
- keep the computer awake (it still locks before any sleep);
- take widgets out of the bar (the markets ticker during a talk).

Four to start with, yours to change or delete: Work, Presentation, Night
and Game. Each one is on by hand (the panel, the palette, the bar), on a
schedule (days and hours; past midnight is fine), or while a second
screen is connected. One at a time; off, what it changed goes back to
how it was (unless you changed it yourself meanwhile: a theme you picked
during the mode stays).

Automatic ones turn off when their time is over. Turned off by hand, one
waits for its next time; one turned on by hand stays until you turn it
off. While one is on, the bar shows it; a click switches or turns it
off.

The modes are kept in `~/.local/state/mazapan-modes/modes.json`. A mode
does what the plugins you have can do: quiet needs `notifications`,
night light `night-light`, keep awake `idle`, the power profile
power-profiles-daemon.
