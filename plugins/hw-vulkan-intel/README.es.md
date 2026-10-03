# Vulkan en gráficos Intel

Vulkan en gráficos Intel, con el driver de Mesa (`vulkan-intel`): los
juegos, Steam y Proton, y las apps que dibujan con Vulkan van en la GPU
en lugar de no arrancar o caer en la CPU. La decodificación de vídeo es
otro plugin, `hw-intel-video`.

Se ofrece en gráficos Intel. No escribe archivos: solo el paquete,
instalado como root. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-vulkan-intel && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo desinstala, salvo que algo
más lo necesite para entonces.
