# Decodificación de vídeo en gráficos Intel

Decodificación de vídeo por hardware (VA-API) en gráficos Intel.
Instala los dos drivers de Intel, `intel-media-driver` (iHD, Broadwell
y posteriores) y `libva-intel-driver` (i965, anteriores), y libva elige
el de tu GPU.

Se ofrece en gráficos Intel. No escribe archivos: solo los dos paquetes,
instalados como root. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-intel-video && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` los desinstala, salvo que
algo más los necesite para entonces.
