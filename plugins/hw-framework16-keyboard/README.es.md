# Iluminación del teclado del Framework Laptop 16

El teclado del Framework Laptop 16 recibe su iluminación RGB y la
configuración de sus teclas por HID en bruto, al que por defecto solo
llega root. Esto te lo da a ti mientras tengas la sesión abierta en el
equipo, para que `qmk_hid` o el configurador VIA en un navegador hablen
con él sin sudo.

Se ofrece en el Framework Laptop 16.

Escribe una regla de udev, como root:
`/etc/udev/rules.d/mazapan-framework16-keyboard.rules`, para el HID en
bruto del teclado (USB `32ac:0012`), que se aplica al momento. `qmk_hid`
no está en los repositorios de Arch (está en el AUR), así que no se
instala. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-framework16-keyboard &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
