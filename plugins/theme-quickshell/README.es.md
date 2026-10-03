# Tema de Quickshell

El shell con el tema: la barra, los paneles, la paleta, las notificaciones
y cualquier otro widget toman sus colores, fuentes, esquinas, bordes y
tiempos de un solo lugar, el tema escrito una vez para todos
(`~/.config/quickshell/mazapan/Theme.qml`).

Un tema nuevo recarga el shell, así se ve al momento. Mientras recorres
temas en el selector, los colores del shell se transforman en los de cada
uno, y vuelven a los tuyos si sales sin elegir. Con el movimiento del tema
desactivado, cambian sin la transición.

Para quien hace plugins: los widgets lo leen tras `import qs` como
`Theme.accent`, `Theme.fgMuted`…; un binding sigue las vistas previas por
sí solo.
