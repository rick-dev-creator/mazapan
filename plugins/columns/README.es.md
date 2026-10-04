# Columnas

Las ventanas una al lado de otra, en columnas sobre una tira que se
desplaza, como lo hace [Niri](https://github.com/YaLTeR/niri), sobre la
disposición de desplazamiento de Hyprland:

- Una ventana nueva se abre a la derecha con la mitad de la pantalla
  (`width`); las demás conservan su ancho y la tira se desplaza hasta la
  que tiene el foco.
- `SUPER + R`: la columna activa pasa por un tercio, la mitad, dos tercios
  y toda la pantalla (`SUPER + SHIFT + R` al revés). `SUPER + F`: todo el
  ancho, y de vuelta. `SUPER + SHIFT + F`: pantalla completa.
- `SUPER + [` / `SUPER + ]`: la ventana entra en la columna de su izquierda
  o derecha (dos ventanas apiladas en una columna) o, si comparte columna,
  sale a una propia.
- `SUPER + Av Pág` / `SUPER + Re Pág`: el espacio de trabajo siguiente o
  anterior de esta pantalla, uno nuevo vacío después del último (con
  `SHIFT`, la ventana va también).
- `SUPER` + la rueda desplaza la tira; con `SHIFT`, cambia de espacio. En
  un touchpad, tres dedos de lado desplazan la tira; arriba y abajo
  cambian de espacio.
- En la barra, junto a los espacios: un bloque por columna, tan ancho como
  ella, los que están en pantalla rellenos y el activo en el color de
  acento; un clic va a esa columna. `SUPER + Tab` (plugin de espacios de
  trabajo) muestra todos los espacios con su tira entera.

Además, tres disposiciones de las de antes, que se recuerdan en cada
espacio de trabajo:

Las ventanas una al lado de otra, en columnas (la disposición de
desplazamiento de Hyprland), en vez de que cada ventana nueva parta en dos
a la anterior. Tres disposiciones, que se recuerdan en cada espacio de
trabajo:

- **Igual** (`SUPER + =`): todas las columnas del mismo ancho, llenando la
  pantalla. Con más ventanas de las que caben a `min_width` (400 píxeles
  lógicos), las columnas conservan ese ancho y la fila se desplaza.
- **Teléfono** (`SUPER + SHIFT + =`): cada columna del ancho de un
  teléfono, y el grupo centrado en la pantalla. Otra vez, y vuelve a
  igual. `phone_aspect` es el ancho de una columna respecto a su alto
  (0.4615, un teléfono en vertical), así que el ancho sigue el alto de cada
  pantalla.
- **Foco** (`SUPER + C`): la ventana activa al centro, con `focus_ratio`
  del ancho (55 %), y las demás apiladas en una columna a cada lado. En
  una ventana lateral, trae esa al centro; en la del centro, sale del modo
  foco.

En la columna activa, `SUPER + ALT + →` y `SUPER + ALT + ←` la ensanchan o
la angostan (en modo foco, la del centro) en `resize_step` (5 % de la
pantalla), nunca por debajo de `min_width`; mantenidas, se repiten.
`SUPER + SHIFT + ←` y `SUPER + SHIFT + →` la mueven un lugar en la fila.
Todas las teclas se pueden cambiar, y también están en la paleta.

Con `auto` activado, las columnas se vuelven a acomodar cada vez que una
ventana se abre, se cierra o llega, para que siempre compartan la pantalla
(del mismo ancho, como antes de que lo de Niri fuera lo normal).
