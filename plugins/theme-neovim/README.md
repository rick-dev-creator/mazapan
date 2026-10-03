# Neovim theme

A Neovim colorscheme made from the theme, `mazapan`: the editor, syntax,
Treesitter, LSP diagnostics, diff and git, the 16 colors of its terminal,
and the plugins people use most (Telescope, which-key, Neo-tree,
nvim-tree, snacks, blink.cmp). When the terminal is see-through, so is the
editor.

It lives in Neovim's data folder (`~/.local/share/nvim/site`), never in
`~/.config/nvim`: that one stays yours (and LazyVim's installer wants it
empty).

`auto` (on): it's used when your config sets no colorscheme. If yours sets
one (LazyVim does), set it to `mazapan` to have the theme. Off, it's there
only for `:colorscheme mazapan`.

When a theme is applied, every open Neovim that uses this colorscheme
loads it again, so the new theme shows at once.
