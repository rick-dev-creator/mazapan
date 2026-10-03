# Wi-Fi 6 en las tarjetas Intel BE200 y BE211

Las tarjetas Wi-Fi 7 BE200 y BE211 de Intel (las llevan los Dell XPS 14
y 16 con Panther Lake, y otros portátiles) todavía no funcionan con
Wi-Fi 7 en Linux: el driver iwlwifi tiene roto el receptor para él, los
puntos de acceso bajan a su velocidad mínima y el Wi-Fi no sirve. Esto
apaga el Wi-Fi 7, y la tarjeta se conecta con Wi-Fi 6, que va a toda
velocidad.

Se ofrece donde hay una de esas tarjetas (PCI 8086:272b, la BE200, o
8086:e440, la BE211).

Escribe una opción de módulo del kernel, como root: `disable_11be=Y`
para iwlwifi, en `/etc/modprobe.d/mazapan-iwlwifi-eht.conf`. Surte
efecto tras reiniciar. Sin ajustes. Es un apaño: cuando Intel arregle
el driver, desactívalo y vuelve el Wi-Fi 7.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-intel-wifi7-eht && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo deshace.
