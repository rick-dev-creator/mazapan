# Night light

The screens warmer at night (`temperature`, 4000 K), fading in over half
an hour after dusk and out before dawn (`fade`), not at once.

When (`schedule`): by the hours you give (`hours`: `from` 20:00, `to`
07:00), or from sunset to sunrise where you are (`sun`, at `sun_at` =
"latitude,longitude"; worked out here, nothing is looked up), or only by
hand (`off`).

"Night light on or off" in the palette switches it now, and it stays so
until the schedule agrees: on in the afternoon, it stays on through the
night; off tonight, it's back tomorrow night.

hyprsunset does the tinting, started with the shell (and gone with it: if
the shell goes, the screens go back to normal). It owns
`~/.config/hypr/hyprsunset.conf`, which stays empty: its profiles would
switch at once and fight with the fade.
