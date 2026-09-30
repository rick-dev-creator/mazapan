# Pantalla de inicio de sesión

La pantalla de inicio de sesión con el tema: el reloj, tu nombre y tu
contraseña, nada más. La inicia greetd, en un Hyprland propio con un
greeter de Quickshell dibujado con los colores del tema, como el usuario
greeter de greetd (no puede leer los archivos de nadie). El teclado es el
del sistema (`/etc/X11/xorg.conf.d/00-keyboard.conf`, lo que escribe
`localectl set-x11-keymap`), así que la contraseña se escribe igual que en
todas partes.

El instalador la activa (sin cifrado de disco: con él, la contraseña del
disco es el inicio de sesión). En un sistema con otro gestor de inicio de
sesión (SDDM, GDM…), activarla lo reemplaza desde el siguiente arranque;
desactivarla deja el inicio de sesión en texto.

Sus archivos son del sistema: `myarch apply --system` los escribe, y un
tema nuevo llega a la pantalla de inicio de sesión con el siguiente
`myarch apply --system`.
