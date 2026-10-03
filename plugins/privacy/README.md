# Privacy dots

A dot in the bar while something listens or watches, as on macOS: orange
for the microphone, green for the camera, blue for the screen shared. A
click says which app. Nothing shows while nothing is in use.

What's in use is read from PipeWire (its running streams and sources, as
Waybar's privacy module does), and cameras opened past PipeWire (some
browsers) count too.

An app that wants to see the screen asks first, as on macOS: Hyprland
shows who is asking, and you deny, allow once, or allow and remember. The
desktop's own tools (capture, recording, the color picker, the bar's
previews, the lock screen) don't ask, and neither does screen sharing
through the portal, which shows its own picker. `screen_ask` (on) turns
it off; a change counts from the next login.

What that holds back, plainly: apps in a sandbox (Flatpak). A program
running as you outside one can use the very tools that don't ask (grim,
for one), as on any Linux desktop; the dialog is for apps, not a wall
against what you run yourself.
