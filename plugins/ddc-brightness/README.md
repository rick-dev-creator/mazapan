# External screens' brightness

The brightness keys change external screens too, over the cable (DDC/CI),
as they change a laptop's own; on a desktop without one, they change the
external ones and show their level. The palette's Brighter and Dimmer do
the same. Most screens answer; some turn DDC/CI off in their own menu.

It installs `ddcutil`, which brings its own udev rule (your session may
reach the screens' I2C buses) and loads `i2c-dev` at every start: it
counts from the next start. Screens are found once every few minutes (it
takes a second or two), and a held key changes them one step at a time.
