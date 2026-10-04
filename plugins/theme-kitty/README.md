# kitty theme

kitty in the theme: the font, the palette (the 16 terminal colors
too), the opacity, the cursor in the accent; open windows reload it at once (SIGUSR1).

It writes its own file, `~/.config/kitty/mazapan-theme.conf`, and one line in yours: `include mazapan-theme.conf` at the top of `kitty.conf`; what follows that line (yours) wins. Your
settings stay yours; turned off, the line goes with the plugin.

Installing it in Apps turns this on.
