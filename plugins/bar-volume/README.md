# Volume

The output's volume in the bar, as an icon and a percentage
(`show_percent`, on). Scroll over it to change it (`step`, 5% a notch), a
middle click mutes or unmutes it. Turning it up unmutes it.

A click opens the card: the output and the input (the microphone), each
with mute and a slider and, when there's more than one, the devices to
pick from. The one you pick becomes the default.

`max_volume` is the highest the slider and the wheel go: 1.0 is 100%; up
to 1.5 boosts quiet sources (it can distort).

It's PipeWire, through WirePlumber. With a sound card, "Check that
everything works" makes sure there's a default output.
