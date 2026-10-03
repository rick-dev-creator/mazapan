# Touchpad del ASUS ROG Flow Z13 al escribir

El teclado desmontable del ASUS ROG Flow Z13 tiene el touchpad visto
como externo, así que libinput no lo empareja con el teclado y no lo
ignora mientras escribes. Al escribir rápido el teclado se flexiona, el
touchpad lo toma por toques y el cursor salta a otra parte. Esto marca
el touchpad como integrado, y el desactivar-al-escribir funciona.

Se ofrece en el ROG Flow Z13 (GZ302), por el nombre que le da el
firmware.

Escribe una regla de udev, como root:
`/etc/udev/rules.d/mazapan-asus-z13-touchpad.rules`, para el touchpad
del teclado (USB `0b05:1a30`). udev la toma al momento; el escritorio,
cuando el teclado se vuelve a conectar o en el siguiente inicio de
sesión. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-asus-z13-touchpad &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
