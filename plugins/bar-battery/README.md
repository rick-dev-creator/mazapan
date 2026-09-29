# Battery

On a laptop (never on a desktop, where there's no battery): the battery's
level in the bar (a bolt while charging, amber when it's getting low, red
when it's almost empty), and a click for more: charging and until when,
or how long it lasts; its health next to when it was new; the power
profile (saver, balanced, performance, with power-profiles-daemon); the
screen's brightness (with a backlight).

A notification when it's getting low (`low`, 15%), an urgent one when
it's almost empty (`critical`, 5%, never above `low`): once each, until
it's plugged in, however many screens (and reloads).
Levels come from UPower; brightness from brightnessctl.
