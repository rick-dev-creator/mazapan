# Ghostty theme

Ghostty in the theme: the font, the palette (the 16 terminal colors
too), the opacity, the cursor in the accent; open windows reload it at once (SIGUSR2).

It writes its own file, `~/.config/ghostty/mazapan-theme`, and one line in yours: `config-file = ?mazapan-theme` at the top of its `config`; Ghostty reads it after your config, so the theme's colors and font win. Your
settings stay yours; turned off, the line goes with the plugin.

Installing it in Apps turns this on.
