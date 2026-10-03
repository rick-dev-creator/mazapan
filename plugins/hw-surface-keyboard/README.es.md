# Teclado del Surface al arrancar

El teclado de los portátiles Microsoft Surface llega al sistema por el
Surface Aggregator, un controlador en una línea serie detrás del LPSS de
Intel. Si se dejan cargar solos, sus módulos aparecen cuando el sistema
ya ha arrancado: demasiado tarde para la contraseña del disco, que se
pide antes. Esto los carga desde el principio.

Se ofrece en los dispositivos Microsoft Surface. Probado por Omarchy, de
donde viene, en el Surface Laptop 3; otros modelos pueden necesitar más.

Escribe un archivo, como root:
`/etc/mkinitcpio.conf.d/mazapan-surface-keyboard.conf`, con el Surface
Aggregator, sus módulos HID y de teclado, los módulos serie del LPSS y
el controlador de pines GPIO (que cambia entre generaciones de Surface:
todos los de Intel, opcionales, y se engancha el que corresponde). Antes,
una comprobación se asegura de que cada kernel instalado tiene esos
módulos. El initramfs se regenera, y
surte efecto tras reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-surface-keyboard && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo deshace.
