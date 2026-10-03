# Volumen de los altavoces en portátiles ASUS ROG

En los portátiles ASUS ROG el mezclador por hardware del códec Realtek
tiene rarezas: los altavoces pueden sonar apagados, y el volumen no se
porta bien. Esto pone el volumen por software (el soft mixer de
WirePlumber), para todas las tarjetas de sonido, y deja en paz el
mezclador por hardware.

Se ofrece en los portátiles ASUS con ROG en el nombre del modelo.

Nada de lo que escribe necesita root: un archivo de WirePlumber,
`~/.config/wireplumber/wireplumber.conf.d/mazapan-asus-rog-soft-mixer.conf`.
Cuando cambia, se borran las rutas guardadas de WirePlumber (llevan
volúmenes para el mezclador por hardware) y WirePlumber se reinicia. Con
un códec ALC285, su control Master, a menudo silenciado de entrada y que
el volumen ya no toca, se quita del silencio al 80%. Instala
`alsa-utils` (para eso, y para conservar el estado del mezclador entre
reinicios). Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-asus-rog-audio && mazapan
apply --system` (como root solo para instalar `alsa-utils`).
`mazapan undo` lo deshace.
