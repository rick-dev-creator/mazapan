# Teclado SPI del MacBook al arrancar

El MacBook y el MacBook Pro de 2015-2017 tienen el teclado y el touchpad
por SPI, y los lleva el driver applespi del kernel. Si se deja que cargue
solo, aparece cuando el sistema ya ha arrancado: demasiado tarde para la
contraseña del disco, que se pide antes. Esto lo carga desde el
principio.

Se ofrece en esos modelos, por el nombre que les da el firmware:
MacBook8,1, MacBook9,1, MacBook10,1, MacBook12,1, MacBookPro13,1–3 y
MacBookPro14,1–3.

Escribe un archivo, como root: applespi y su controlador SPI en el
initramfs, en `/etc/mkinitcpio.conf.d/mazapan-apple-spi-keyboard.conf`
(el controlador del MacBook8,1 es un dispositivo PCI; los modelos
posteriores llegan a él por el LPSS de Intel). Antes, una comprobación
se asegura de que cada kernel instalado tiene esos módulos. El initramfs se
regenera, y surte efecto tras reiniciar. Sin ajustes.

La Touch Bar no está incluida: necesita un driver de fuera del kernel,
en el AUR.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-apple-spi-keyboard &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
