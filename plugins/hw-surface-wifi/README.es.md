# Firmware Wi-Fi del Surface

Muchos dispositivos Microsoft Surface tienen Wi-Fi y Bluetooth de
Marvell, cuyo firmware Arch guarda en un paquete aparte,
`linux-firmware-marvell`, que `linux-firmware` no trae. Sin él no hay
Wi-Fi.

Se ofrece en los dispositivos Microsoft Surface (en uno con Wi-Fi de
otro fabricante no hace daño: son solo archivos de firmware).

No escribe archivos: solo instala `linux-firmware-marvell`, como root.
El Wi-Fi aparece tras reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-surface-wifi && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo desinstala, salvo que
para entonces otra cosa lo necesite.
