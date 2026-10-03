# NVIDIA (Turing y posteriores)

El driver abierto de NVIDIA, para GeForce RTX 20 y posteriores. Se
ofrece donde la GPU es una NVIDIA de Turing en adelante: esas corren los
módulos de kernel abiertos de NVIDIA.

Lo que hace, como root:
- Instala `nvidia-open-dkms`, `nvidia-utils`, `libva-nvidia-driver` y
  `linux-headers`. El driver se compila para tu kernel al instalarse,
  así que cada kernel instalado necesita sus headers; una comprobación
  los busca antes de escribir los archivos de abajo.
- Dos archivos del kernel: el modesetting activado (con la consola
  también en el driver de NVIDIA), en
  `/etc/modprobe.d/mazapan-nvidia.conf`, y los módulos de NVIDIA en el
  initramfs, en `/etc/mkinitcpio.conf.d/mazapan-nvidia.conf`, para que
  el driver arranque antes que la pantalla. El initramfs se regenera, y
  surte efecto tras reiniciar.

Y las variables de Hyprland, en `~/.config/hypr/mazapan/hw-nvidia.lua`.
Donde la GPU de NVIDIA es la única, la decodificación de vídeo y GLX van
por su driver. Con varias GPU no se pone nada: las apps se quedan en la
que lleva las pantallas, para que la de NVIDIA no se mantenga despierta
por todo y la decodificación de vídeo siga funcionando en la otra. Sin
ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-nvidia && mazapan apply
--system`. Lo que corre como root se lista antes, y pide tu contraseña
en una terminal; `mazapan undo` lo deshace.
