# Learn

Short lessons on moving around Mazapan, and every key on screen while
SUPER is held.

## The lessons

`SUPER + F1`, the Learn tile on the palette's first screen, or the
palette's Learn filter. Each lesson is a few seconds: a real recording of
the move, its keys lit as it happens, and a line on why it's useful.

- Windows in columns: look around, move a window between columns, make it
  wider, even them out, one window on all of the screen.
- Workspaces: one after another, and the overview.
- Everyday: the command palette, and every key.

**Try it** steps aside to a small card at the bottom: practice windows
open on a workspace of their own, you make the move with the real keys,
and it's checked off when it's done (the card never takes the keyboard).
Then the panel comes back on the next lesson. What's done is kept
(`~/.local/state/mazapan/learn.json`).

The keys shown are the ones you have: change one in Settings › Keys and
the lesson shows it changed. In the palette, an action a lesson teaches
(moving a window, the widths…) shows its recording beside it.

## Every key

Hold SUPER alone for a moment (`hold_ms`, 700) and every key shows,
grouped (windows and columns, workspaces, open, everyday), read from every
plugin's actions. Let go and it's gone; a shortcut pressed meanwhile
calls it off, so SUPER works as always. `SUPER + K` shows it and hides it
again. `hold_super = false` turns the hold off.

## The recordings

`media/ID.webp`, made in the dev VM by `vm/learn-record` (the steps'
seconds in `lessons.json.tmpl` match its timeline). Animated WebP, read
with qt6-imageformats; they ship with the plugin, so nothing is
downloaded.
