# Tema de Neovim

Un esquema de colores de Neovim hecho a partir del tema, `mazapan`: el
editor, la sintaxis, Treesitter, los diagnósticos de LSP, diff y git, los
16 colores de su terminal, y los plugins más usados (Telescope, which-key,
Neo-tree, nvim-tree, snacks, blink.cmp). Cuando el terminal es
translúcido, el editor también.

Vive en la carpeta de datos de Neovim (`~/.local/share/nvim/site`), nunca
en `~/.config/nvim`: esa sigue siendo tuya (y el instalador de LazyVim la
quiere vacía).

`auto` (activado): se usa cuando tu configuración no elige esquema de
colores. Si la tuya elige uno (LazyVim lo hace), ponlo en `mazapan` para
tener el tema. Desactivado, solo está ahí para `:colorscheme mazapan`.

Al aplicar un tema, cada Neovim abierto que usa este esquema lo vuelve a
cargar, así el tema nuevo se ve al momento.
