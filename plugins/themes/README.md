# Theme picker

How the whole desktop looks, chosen in one place: `SUPER + SHIFT + T`
(`key`), or "Theme" on the palette's first screen.

Every theme shows as a small desktop drawn from its own colors: a bar, a
terminal in its 16 colors, a window with a selection and buttons. Your own
themes get an exact preview too.

- ← → (or h, l) go through the themes, each one previewed live on the real
  desktop: the bar, panels, wallpaper and window borders change to it, and
  the terminals already open take its colors. Other apps follow once you
  apply it.
- Tab (or ↓ ↑) picks the accent: the theme's own, then the ones it
  suggests. The colors that go with it (text on it, links, the selection)
  are worked out from it with their contrast kept. Your accent carries to
  every theme you look at.
- ↵, or a click on the chosen theme, applies it everywhere. Esc goes back
  to yours, with nothing left changed.

Below the themes: "contrast ok", or which colors read too poorly on which
("fg on bg is 4.3:1, needs 4.5:1"); and how many of your installed apps the
theme reaches, naming the ones it doesn't yet.

If a theme can't be applied, everything goes back as it was and a notice
says so; what went wrong is in `~/.local/state/mazapan/theme.log`.

"List themes and their contrast", in the palette, shows every theme in a
terminal (`mazapan themes`).

## Your own themes

A theme is a folder with one file, `theme.toml`, in
`~/.local/share/mazapan/themes/`; the folder's name is the theme's id
(lowercase letters, digits and dashes). The file has `[meta]` (its name,
dark or light, the accents it suggests), `[colors]` by what they're for
(`bg`, `fg`, `accent`, `danger`…), `[ansi]` for terminals, `[font]`,
`[shape]` (corners, borders, gaps), `[motion]`, and optionally `[effects]`
(see-through terminals, blur, the wallpaper). The bundled ones (Amber,
Gruvbox, Paper, Phosphor) are the examples to start from.

The picker reads the themes each time it opens. One of yours with a
bundled theme's id takes its place.

A theme can also be made from a picture: "Theme from this picture" in the
wallpaper picker, or `mazapan themes from-image PICTURE`.
