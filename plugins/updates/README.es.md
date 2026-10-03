# Actualizaciones

Actualizaciones a la vista. La barra muestra una flecha y cuántas hay
(los paquetes de todo el sistema y las apps Flatpak), buscadas cada
pocas horas; el panel muestra qué cambia (★ de lo que depende tu
escritorio), las noticias de Arch que leer antes de actualizar (! las
que piden hacer algo) y si un kernel nuevo necesita reiniciar.
"Actualizar ahora" corre `mazapan update` con la contraseña en el diálogo
de polkit: un snapshot antes, las comprobaciones del escritorio después,
y vuelta atrás si fallan.

Mientras corre, el panel muestra sus pasos, cada uno con su ✓
(preparando, las claves, los paquetes, la configuración, las
comprobaciones), y ofrece reiniciar cuando cambió el kernel o Hyprland.
Cuando la actualización trae un Mazapán nuevo, primero dice qué trae de
nuevo, según su registro de cambios.
