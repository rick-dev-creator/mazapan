# Altavoces de graves del Lenovo Yoga Pro 7

El Lenovo Yoga Pro 7 (14IAH10) tiene altavoces de graves que se quedan
mudos sin el modelo de pines correcto para su códec de sonido. Esto le
da ese modelo, y suenan.

Se ofrece solo en ese modelo (el Yoga Pro 7 14IAH10, por el nombre que
le da el firmware).

Escribe una opción de módulo del kernel, como root: `hda_model` para el
driver de sonido (`snd-sof-intel-hda-generic`), en
`/etc/modprobe.d/mazapan-yoga-pro7-bass.conf`. Surte efecto tras
reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-yoga-pro7-bass && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo deshace.
