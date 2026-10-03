# Cursor with several GPUs

The mouse cursor drawn in software, for machines with several GPUs where
one renders and another drives screens: a dock on the iGPU, a laptop's
own panel. A frame made on one GPU and shown by another is copied
between them, and the hardware cursor makes those screens flicker as the
mouse moves and while typing. Drawn in software, the cursor is part of
the frame instead.

Offered where there are at least two GPUs. Nothing runs as root: it's
one line of Hyprland's config, `~/.config/hypr/mazapan/hw-cursor.lua`,
taken at once. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-multi-gpu-cursor && mazapan apply`.
