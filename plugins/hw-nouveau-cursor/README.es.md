# Cursor con el driver nouveau

El cursor del ratón dibujado por software donde una GPU NVIDIA usa
nouveau, el driver abierto que trae el kernel. En muchas GPU NVIDIA
antiguas nouveau no muestra el cursor por hardware, así que con Hyprland
el puntero es invisible. Dibujado por software, el cursor pasa a ser
parte del fotograma.

Se ofrece donde el driver nouveau está cargado. Donde se configura en su
lugar el driver propio de NVIDIA (`hw-nvidia` activado: al instalar,
nouveau sigue en marcha hasta el primer reinicio), no hace nada, porque
ese driver sí muestra el cursor por hardware; lo mismo cuando ya ninguna
GPU usa nouveau.

No corre nada como root: es una línea de la configuración de Hyprland,
`~/.config/hypr/mazapan/hw-nouveau-cursor.lua`, que se aplica al
momento. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-nouveau-cursor && mazapan
apply`.
