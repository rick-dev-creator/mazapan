# Selector de temas

El aspecto de todo el escritorio, elegido en un solo lugar:
`SUPER + SHIFT + T` (`key`), o "Tema" en la primera pantalla de la paleta.

Cada tema se muestra como un pequeño escritorio dibujado con sus propios
colores: una barra, un terminal con sus 16 colores, una ventana con una
selección y botones. Tus propios temas también tienen una vista previa
exacta.

- ← → (o h, l) recorren los temas, cada uno mostrado en vivo en el
  escritorio real: la barra, los paneles, el fondo de pantalla y los bordes
  de las ventanas cambian a él, y los terminales ya abiertos toman sus
  colores. Las demás apps lo siguen al aplicarlo.
- Tab (o ↓ ↑) elige el color de acento: el del tema, y luego los que
  sugiere. Los colores que van con él (el texto encima, los enlaces, la
  selección) se calculan a partir de él manteniendo su contraste. Tu acento
  te acompaña en cada tema que miras.
- ↵, o un clic en el tema elegido, lo aplica en todas partes. Esc vuelve al
  tuyo, sin dejar nada cambiado.

Debajo de los temas: "contraste ok", o qué colores se leen mal sobre cuáles
("fg sobre bg da 4.3:1, necesita 4.5:1"); y a cuántas de tus apps
instaladas llega el tema, nombrando las que aún no.

Si un tema no se puede aplicar, todo vuelve a como estaba y un aviso lo
dice; lo que falló está en `~/.local/state/mazapan/theme.log`.

"Listar temas y su contraste", en la paleta, muestra cada tema en un
terminal (`mazapan themes`).

## Tus propios temas

Un tema es una carpeta con un archivo, `theme.toml`, en
`~/.local/share/mazapan/themes/`; el nombre de la carpeta es el id del tema
(minúsculas, dígitos y guiones). El archivo tiene `[meta]` (su nombre,
oscuro o claro, los acentos que sugiere), `[colors]` según para qué sirven
(`bg`, `fg`, `accent`, `danger`…), `[ansi]` para los terminales, `[font]`,
`[shape]` (esquinas, bordes, huecos), `[motion]` y, si quieres,
`[effects]` (terminales translúcidos, desenfoque, el fondo de pantalla).
Los incluidos (Amber, Gruvbox, Paper, Phosphor) son los ejemplos de los
que partir.

El selector lee los temas cada vez que se abre. Uno tuyo con el id de un
tema incluido ocupa su lugar.

También se puede hacer un tema a partir de una foto: "Tema desde esta
foto" en el selector de fondos, o `mazapan themes from-image FOTO`.
