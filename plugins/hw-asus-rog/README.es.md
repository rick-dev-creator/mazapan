# Controles de los portátiles ASUS ROG

asusctl, para los portátiles ASUS ROG: perfiles de rendimiento, curvas
de ventilador, un límite de hasta dónde carga la batería y la
iluminación del teclado, con `asusctl` y su servicio, asusd.

Se ofrece en los portátiles ASUS con ROG en el nombre del modelo (casi
todos; a un ROG antiguo cuyo nombre no lo diga no se le ofrece).

No escribe archivos: solo instala `asusctl`, de los repositorios de
Arch, como root. asusd lo arranca su propia regla de udev en los equipos
ASUS, así que corre desde el siguiente arranque; una comprobación dice
si lo hace. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-asus-rog && mazapan apply
--system`. Lo que corre como root se lista antes, y pide tu contraseña
en una terminal; `mazapan undo` lo desinstala, salvo que para entonces
otra cosa lo necesite.
