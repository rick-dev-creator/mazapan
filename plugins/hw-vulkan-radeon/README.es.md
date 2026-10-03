# Vulkan en gráficos AMD

Vulkan en gráficos AMD Radeon, con el driver RADV de Mesa
(`vulkan-radeon`): los juegos, Steam y Proton, y las apps que dibujan
con Vulkan van en la GPU en lugar de no arrancar o caer en la CPU. La
decodificación de vídeo en AMD no necesita nada más: Mesa ya la trae.

Se ofrece en gráficos AMD, integrados o en tarjeta. No escribe archivos:
solo el paquete, instalado como root. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-vulkan-radeon && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo desinstala, salvo que algo
más lo necesite para entonces.
