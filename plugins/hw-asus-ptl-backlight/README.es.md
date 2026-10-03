# Brillo de pantalla en ASUS con Panther Lake

En el ASUS ExpertBook B9406 y el Zenbook UX5406AA (Intel Panther Lake)
las teclas y el control de brillo no hacen nada intermedio: la pantalla
está al máximo o apagada. El EDID del panel se lee vacío, así que el
driver xe lleva la retroiluminación como dice la tabla del firmware
(PWM), cuando el panel la quiere por DisplayPort AUX (DPCD). Esto le
dice a xe que use DPCD, y el brillo vuelve a cambiar por pasos.

Se ofrece en esos dos modelos con gráficos Panther Lake. Puede que otros
portátiles ASUS con Panther Lake tengan el mismo problema; no se les
ofrece hasta que se sepa.

Escribe una opción de módulo del kernel, como root: el
`enable_dpcd_backlight=1` del driver xe, en
`/etc/modprobe.d/mazapan-asus-ptl-backlight.conf`. El driver arranca
desde el initramfs, así que se regenera, y surte efecto tras reiniciar.
Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-asus-ptl-backlight &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
