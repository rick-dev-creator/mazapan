# La pantalla del ASUS ExpertBook B9406 se sigue actualizando

En el ASUS ExpertBook B9406 (Intel Panther Lake, gráficos Xe3) la
pantalla se congela en el último fotograma y solo cambia con un
modeset completo, como pasar a una consola de texto y volver. Panel
Replay, nuevo en Xe3 y activado por defecto, nunca despierta este panel.
Esto lo desactiva, y la pantalla vuelve a seguir lo que se dibuja.

Se ofrece en el ExpertBook B9406 con gráficos Panther Lake.

Escribe una opción de módulo del kernel, como root: el
`enable_panel_replay=0` del driver xe, en
`/etc/modprobe.d/mazapan-asus-b9406-panel-replay.conf` (desactivar solo
PSR no cubre Panel Replay). El driver arranca desde el initramfs, así
que se regenera, y surte efecto tras reiniciar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-asus-b9406-panel-replay &&
mazapan apply --system`. Lo que corre como root se lista antes, y pide
tu contraseña en una terminal; `mazapan undo` lo deshace.
