# Alacritty theme

Alacritty in the theme: the font, the palette (the 16 terminal colors
too), the opacity, the cursor in the accent; Alacritty reloads it by itself.

It writes its own file, `~/.config/alacritty/mazapan-theme.toml`, and one line in yours: `general.import = [...]` at the top of `alacritty.toml`; what your file sets after it wins (if you have a `[general]` table of your own, put `import` there instead). Your
settings stay yours; turned off, the line goes with the plugin.

Installing it in Apps turns this on.
