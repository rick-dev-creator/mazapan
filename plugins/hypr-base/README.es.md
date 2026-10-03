# Base de Hyprland

Donde arranca el escritorio: la configuración de Hyprland,
`~/.config/hypr/hyprland.lua`. Ajusta las pantallas (cada una en su modo
preferido, colocada y escalada de forma automática), el teclado, el
ratón y el touchpad, y las teclas de abajo, y luego carga lo que cada
otro plugin pone en `~/.config/hypr/mazapan/`. Cada uno se carga por su
cuenta: uno roto no impide que carguen los demás, y Hyprland muestra su
error.

El archivo se reescribe en cada `mazapan apply`: lo que edites en él no
dura. Una comprobación se asegura de que Hyprland lo acepta, también
tras cada actualización.

## Teclas

- `SUPER + Return`: una terminal (foot).
- `SUPER + Q`: cerrar la ventana, o el panel abierto si hay uno.
- `SUPER + V`: hacer flotante o acomodar la ventana.
- `SUPER + J`: cambiar la dirección de la división.
- `SUPER + ← ↑ → ↓`: enfocar la ventana de ese lado.
- `SUPER + 1…0`: ir al workspace 1 a 10.
- `SUPER + SHIFT + 1…0`: mover la ventana al workspace 1 a 10.
- `SUPER + SHIFT + E`: salir de Hyprland.

## Ajustes

- `kb_layout` ("us"): las distribuciones del teclado, separadas por
  comas ("us,es"), con los nombres de xkb; `kb_variant` (ninguna) la
  variante de cada una, en el mismo orden ("intl"); `kb_options`
  (ninguna) las opciones de xkb, como una tecla para cambiar de
  distribución ("grp:alt_shift_toggle"; con varias distribuciones y sin
  esa tecla, no hay forma de cambiar).
- Touchpad: `tap_to_click` (activado), un toque es un clic y con dos
  dedos el derecho; `natural_scroll` (desactivado), el contenido se
  mueve con los dedos, como en un teléfono; `disable_while_typing`
  (activado), para que la palma no mueva el puntero.
- `pointer_speed` (0), ratón y touchpad: -1 lo más lento, 1 lo más
  rápido.

El teclado y el touchpad también están en Ajustes (`SUPER + comma`).
