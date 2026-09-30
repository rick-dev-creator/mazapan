# Wallpaper

What the desktop shows behind the windows.

- **The theme's own**: drawn from its colors (a glowing grid in dark
  themes, graph paper in light ones), or the theme's picture. It changes
  with the theme, previews included.
- **Yours**: put pictures in `~/Pictures/Wallpapers` (`folder`) and open
  the picker (`SUPER + SHIFT + W`). The one under the pointer is on the
  desktop at once; a click keeps it, for every screen or for one. Fill,
  fit, center or tile; tinted with the theme (subtle or strong) so any
  picture belongs; a new one every 15 minutes, hour or day
  (`SUPER + ALT + W` for the next one now).
- **Day and night**: `name-day.jpg` and `name-night.jpg` are one picture
  that follows the time (`day_from`, `night_from`).
- **A theme from any picture**: "Theme from this picture" (or `t`) makes a
  whole theme of its colors, every contrast checked, and switches to it:
  the terminal, GTK and Qt apps, the browsers, VS Code, Neovim and the
  shell, all in the picture's colors. Also `mazapan themes from-image
  PICTURE [--apply]`; `mazapan themes remove ID` takes one away.

Your choice is kept in `~/.local/state/mazapan-wallpaper/`: changing it
reloads nothing. Reading a picture's colors needs ffmpeg (or ImageMagick).
