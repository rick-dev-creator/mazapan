# Wi-Fi Broadcom BCM4360 y BCM4331

El Broadcom BCM4360 (MacBooks de 2013-2015) y el BCM4331 (MacBooks de
2012 y principios de 2013), que también llevan otros portátiles, sacan
poco o ningún Wi-Fi de los drivers del propio kernel. El driver wl de
Broadcom sí los lleva.

Se ofrece donde está uno de esos dos chips (PCI `14e4:43a0` o
`14e4:4331`). Los chips Broadcom más nuevos de los Mac van con brcmfmac
(ver `hw-apple-brcmfmac-wpa`).

No escribe archivos: instala `broadcom-wl-dkms` y `linux-headers`, como
root. El driver se compila para tu kernel al instalarse, así que cada
kernel instalado necesita sus headers; una comprobación los busca antes.
El paquete impide que carguen los drivers Broadcom del propio kernel, y
el Wi-Fi aparece con wl tras reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-broadcom-wl && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` los desinstala, salvo que
para entonces otra cosa los necesite.
