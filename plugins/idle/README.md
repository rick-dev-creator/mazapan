# Idle

What happens after a while without use:
- The screens dim for 15 seconds first (`dim`); a move and they're back.
- Then it locks (`lock`, 5 minutes).
- The screens turn off (`screens_off`, 6).
- It suspends on battery (`suspend_on_battery`, 15), and when plugged in
  or on a desktop only if you ask (`suspend`, 0: never).

It locks before any sleep, however it comes (`lock_on_sleep`: the lid, a
key, the palette), and the screens come back on after.

An app that asks to keep the screen on is listened to: a video playing
in the browser, a call, a presentation. You can ask too: "Keep awake" in
the palette, and a cup shows in the bar while nothing dims, locks or
suspends by itself (it still locks before any sleep). A click on the cup, or the same action, and it's off
again; a reboot turns it off as well.

It's hypridle (`~/.config/hypr/hypridle.conf`), started with Hyprland.
Turned off, it stops at the next login.
