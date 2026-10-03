# Teclas de función primero en teclados Apple

F1–F12 como teclas de función, y las multimedia con Fn, en los teclados
que lleva el driver hid_apple: los de Apple, y otros en modo Mac
(Keychron…). Si no se toca, el kernel adivina cuáles van primero.

Se ofrece donde el driver hid_apple está cargado. Una vez activado,
sigue activado aunque desconectes el teclado.

Escribe una opción de módulo del kernel, como root: el `fnmode` de
hid_apple, en `/etc/modprobe.d/mazapan-hid-apple.conf`, para cada
arranque. El driver ya cargado la toma al momento, sin reiniciar.

`fnmode` (2): 2, primero las teclas de función; 1, primero las
multimedia; 3, lo que decida el kernel (su valor por defecto); 0, sin
tecla Fn.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-apple-fnkeys && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo deshace.
