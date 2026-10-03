# Contraseñas Wi-Fi en Macs con Wi-Fi Broadcom

En los Mac cuyo Wi-Fi Broadcom lleva el driver brcmfmac, el firmware de
la tarjeta hace él mismo el handshake WPA, y contra un punto de acceso
en modo de transición WPA2/WPA3 no llega a terminarlo: el Mac se asocia
y NetworkManager dice que la contraseña es incorrecta. Esto le devuelve
el handshake a wpa_supplicant, y la contraseña correcta funciona.

Se ofrece en los Mac con uno de los chips Wi-Fi que lleva brcmfmac: el
BCM43602 de 2015-2017, los BCM4350, BCM4355 y BCM4364 de 2018-2019, y
los BCM4377, BCM4378 y BCM4387 de la época T2 en adelante (también todo
Mac con T2). El BCM4360 de 2013-2015 no es uno de ellos: lo lleva el
driver wl (`hw-broadcom-wl`).

Escribe una opción de módulo del kernel, como root: el
`feature_disable=0x82000` de brcmfmac (sin el supplicant ni el
autenticador del firmware), en
`/etc/modprobe.d/mazapan-apple-brcmfmac.conf`. Surte efecto tras
reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-apple-brcmfmac-wpa &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
