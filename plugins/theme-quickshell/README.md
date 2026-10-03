# Quickshell theme

The shell in the theme: the bar, the panels, the palette, notifications
and every other widget take their colors, fonts, corners, borders and
timing from one place, the theme written once for all of them
(`~/.config/quickshell/mazapan/Theme.qml`).

A new theme reloads the shell, so it shows at once. While you go through
themes in the picker, the shell's colors morph to each one, and back to
yours if you leave without choosing. With the theme's motion off, they
change without the fade.

For plugin authors: widgets read it after `import qs` as `Theme.accent`,
`Theme.fgMuted`…; a binding follows previews by itself.
