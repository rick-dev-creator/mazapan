# Workspaces

The workspace numbers in the bar: 1 to 5 always (`persistent`), any other
while it exists, 10 shown as 0 (its key). The one you're on is a solid
block in the accent color that slides from number to number; one showing
on another monitor is underlined; empty ones are dimmed. A click on a
number goes there; scrolling over them steps through them. With the
Agents plugin, a dot on a number says a coding agent's session is there:
amber while it waits for you, the text's color while it works, green when
it's done.

A right click on a number opens a live preview of that workspace (again
to close), every window at its real place and size; it grows a moment
later. While it's open, pointing at another number shows that one. With
`trigger` set to `hover`, pointing at a number opens it (after
`open_delay_ms`, 350 ms). There's no preview of what this monitor already
shows. Turn it off with `preview`.

In the preview, a click on a window goes to it. Its windows can be dragged
out: dropped on the screen, a window comes to the workspace you're on and
gets the focus; dropped on another number, it moves there without taking
you along; dropped back on the card, nothing happens.

The rest is size: `cell` (28) and `font_size` (9.5 points) for the
numbers; `preview_width` (320) and `preview_width_large` (640) for the
card, small and grown, after `grow_delay_ms` (200 ms); `preview_aspect`
(1.6) keeps it at least that landscape, while an ultrawide keeps its own
shape.

## Overview

`SUPER + Tab` (`overview_key`) shows every workspace of the screen at once,
as Niri's overview: each with its windows live, the whole strip of
columns (the part on the screen outlined), and an empty one after the
last. A click on a window goes to it; on a workspace, there. A window
dragged onto another workspace goes there. `1`…`9` go to that workspace;
Esc closes it.
