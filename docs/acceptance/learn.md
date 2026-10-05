# Acceptance: Learn

What the Learn plugin has to do before it's called done, and how each
point is checked. `vm/accept-learn` checks every one marked **auto** in
the dev VM, with real keys (QEMU's keyboard) on its Latin American
layout, and fails on the first that doesn't hold; the others are looked
at on its screenshots.

| # | Criterion | How |
|---|---|---|
| 1 | The plugin renders in every theme and language, no error, no warning | auto: `mazapan plugins check learn` |
| 2 | Nine lessons, each with its recording there and animated (an animated WebP) | auto: `lessons.json`, each `media/ID.webp` |
| 3 | A lesson shows the key as it's bound now: a key changed in config.toml shows changed | auto: `move_right_key` changed, applied, read back, put back |
| 4 | SUPER + F1 opens the Learn panel; Esc closes it | auto: the `mazapan-panel-learn` layer |
| 5 | SUPER held alone shows every key; letting go hides them | auto: the `mazapan-panel-keys` layer, held 1.5 s |
| 6 | A shortcut with SUPER (SUPER + →) never shows the keys, and holding SUPER after one still does | auto |
| 7 | SUPER + K shows the keys and hides them again | auto |
| 8 | Every lesson's "Try it" steps the panel aside for its practice card (the keyboard left free), opens its practice, ticks it off when its move is made with the real keys, closes the practice windows and comes back to the workspace it started on | auto: each of the nine, `learn.json` |
| 9 | The shell never goes down: alive after every step above | auto: `qs ipc … ping` |
| 10 | Recording the screen while the overview shows windows, then closing them, doesn't bring the shell down (found while recording the lessons) | auto |
| 11 | The palette's Learn filter lists the lessons; an action a lesson teaches shows its recording and "Watch how" | screenshot |
| 12 | The welcome's last step offers the tour | auto: the welcome rendered with it |
| 13 | The steps light up as their move happens in the recording | screenshot |
| 14 | The columns' keys can be pressed on a keyboard where `=`, `[` and `]` need SHIFT or ALTGR (the VM's is Latin American): SUPER + W evens the widths (8, equal), SUPER + P the phone widths, SUPER + CTRL + → puts the window into the next column | auto |
| 15 | Stopping a practice while its windows are still opening (SUPER + F1) leaves none behind, and comes back to the workspace it started on | auto |
| 16 | SUPER + Q in a practice stops the practice (not one of its windows), and doesn't tick it off | auto |
| 17 | A lesson isn't ticked off before its move is made; the palette's, the overview's and the keys' are done once opened and closed again | auto (in 8) |
