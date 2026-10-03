# Touchpads Synaptics por SMBus

Touchpads Synaptics por InterTouch (SMBus) en lugar de PS/2:
desplazamiento suave y gestos, en ThinkPads y otros portátiles.

Se ofrece donde el touchpad aparece como "SynPS/2 Synaptics TouchPad",
es decir, todavía por PS/2 (uno que ya va por SMBus tiene otro nombre).

Escribe una opción de módulo del kernel, como root:
`synaptics_intertouch=1` para psmouse, en
`/etc/modprobe.d/mazapan-psmouse.conf`. Surte efecto tras reiniciar.
Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-synaptics-intertouch &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
