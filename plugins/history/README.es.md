# Historial

Lo que cambió en el escritorio, en palabras, lo más reciente primero,
agrupado por día (`SUPER + ALT + H`, o "Historial" en la paleta):
- un tema o acento ("Tema: Gruvbox → Paper", con ambas paletas);
- un ajuste ("Volumen: Volumen máximo predeterminado (1) → 1.25");
- un plugin activado o desactivado;
- archivos reescritos sin cambios en tus ajustes (la nueva versión de un
  plugin), por plugin;
- una actualización (`mazapan update`), y cómo fue;
- un snapshot del sistema, con `hw-snapshots`: el sistema antes y
  después de un cambio de paquetes, arrancable desde el menú de arranque
  con `hw-snapshots-grub`.

Cada uno tiene su propio deshacer. Un cambio anterior se deshace por
separado: vuelve lo que cambió y sigue como lo dejó (un ajuste de esta
mañana, sin perder el tema que elegiste después), con un nuevo apply que
también aparece aquí. Solo archivos: los del último cambio. Una
actualización: se revierte en una terminal (pide tu contraseña).

Desde una terminal: `mazapan timeline` y `mazapan timeline undo ID`.
