# Columnas iguales

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

Las columnas se vuelven a acomodar cada vez que una ventana se abre, se
cierra o llega, para que siempre compartan la pantalla (`auto`,
activado). Desactívalo para conservar columnas que ajustaste a mano:
entonces solo las teclas las reordenan.
