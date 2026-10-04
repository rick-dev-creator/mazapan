# Tema de Alacritty

Alacritty con el tema: la fuente, la paleta (también los 16 colores de
terminal), la opacidad, el cursor en el acento; Alacritty lo recarga solo.

Escribe su propio archivo, `~/.config/alacritty/mazapan-theme.toml`, y una línea en el tuyo: encima de `alacritty.toml`, la línea `general.import = [...]`; lo que tu archivo pone después gana (si tienes tu propia tabla `[general]`, pon `import` ahí).
Tus ajustes siguen siendo tuyos; al desactivarlo, la línea se va con el
plugin.

Instalarla desde Apps lo activa.
