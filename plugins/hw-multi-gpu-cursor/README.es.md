# Cursor con varias GPU

El cursor del ratón dibujado por software, para equipos con varias GPU
donde una renderiza y otra lleva pantallas: un dock en la iGPU, la
pantalla de un portátil. Un fotograma hecho en una GPU y mostrado por
otra se copia entre ellas, y el cursor por hardware hace parpadear esas
pantallas al mover el ratón y al escribir. Dibujado por software, el
cursor pasa a ser parte del fotograma.

Se ofrece donde hay al menos dos GPU. Nada corre como root: es una línea
de la configuración de Hyprland, `~/.config/hypr/mazapan/hw-cursor.lua`,
que se toma al momento. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-multi-gpu-cursor && mazapan
apply`.
