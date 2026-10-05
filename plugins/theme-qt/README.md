# Qt theme

Qt 6 apps in the theme, KDE's too (Dolphin, Kdenlive, Okular…): their
palette, fonts and widget style through qt6ct, and KDE's color scheme,
which KDE apps add on top. Hyprland is told to have Qt apps use qt6ct.
Qt 5 apps aren't reached.

`style` (Fusion) is Qt's widget style: Fusion follows the palette in full.
`icon_theme` (Adwaita) is the icons Qt apps use.

Qt apps read all this when they start: open ones keep their colors until
they're opened again. qt6ct and KDE apps write to the same files
(`~/.config/qt6ct/qt6ct.conf`, `~/.config/kdeglobals`): only the keys
Mazapan writes are its own, the rest stay theirs and yours.

It needs qt6ct; its check says so when it isn't installed.
