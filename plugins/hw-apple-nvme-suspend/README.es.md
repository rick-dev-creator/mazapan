# El NVMe del MacBook despierta tras suspender

El disco NVMe del MacBook y el MacBook Pro de 2015-2017 no consigue
despertar de su estado de energía más profundo (D3cold): tras suspender,
el sistema no llega a su disco. Esto mantiene el disco fuera de ese
estado; sigue durmiendo, solo que no tan profundo.

Se ofrece en esos modelos, por el nombre que les da el firmware:
MacBook8,1, MacBook9,1, MacBook10,1, MacBookPro13,1–3 y
MacBookPro14,1–3.

Escribe un archivo, como root:
`/etc/udev/rules.d/mazapan-apple-nvme-suspend.rules`, que pone a 0 el
`d3cold_allowed` del controlador NVMe de Apple (S1X o S3X, reconocido por
su id PCI, esté donde esté en el bus) en cada arranque. También se pone al momento, sin reiniciar;
al desactivarlo, el disco vuelve a poder entrar en D3cold. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-apple-nvme-suspend &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
