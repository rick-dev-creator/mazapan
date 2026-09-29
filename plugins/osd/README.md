# Media keys

The volume, microphone, brightness and media keys work (on the lock screen
too; held down, they repeat), and what they did shows for a moment: a
small card on the focused screen with the level, or the track. Raising the
volume unmutes it, as on a phone. The volume card also shows when
something else changes it (the bar, an app). Clicks go through the card.

Volume and the microphone go through `wpctl` (PipeWire), brightness
through `brightnessctl` (`brightness_step`), media through MPRIS (the
player that's playing, else the first). `max_volume` above 1.0 boosts
quiet sources (it can distort).
