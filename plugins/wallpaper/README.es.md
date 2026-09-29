# Fondo de pantalla

Lo que muestra el escritorio detrás de las ventanas.

- **El del tema**: dibujado con sus colores (una rejilla luminosa en los
  temas oscuros, papel milimetrado en los claros), o la imagen del tema.
  Cambia con el tema, vistas previas incluidas.
- **Los tuyos**: pon fotos en `~/Pictures/Wallpapers` (`folder`) y abre el
  selector (`SUPER + SHIFT + W`). La que está bajo el puntero se ve en el
  escritorio al momento; un clic la deja, en todas las pantallas o en una.
  Rellenar, ajustar, centrar o mosaico; teñida con el tema (suave o
  fuerte) para que cualquier foto encaje; una nueva cada 15 minutos, hora
  o día (`SUPER + ALT + W` para la siguiente ya).
- **Día y noche**: `nombre-day.jpg` y `nombre-night.jpg` son una sola foto
  que sigue la hora (`day_from`, `night_from`).
- **Un tema desde cualquier foto**: "Tema desde esta foto" (o `t`) hace un
  tema completo con sus colores, con todos los contrastes comprobados, y lo
  pone: el terminal, las apps GTK y Qt, los navegadores, VS Code, Neovim y
  el shell, todo con los colores de la foto. También `myarch themes
  from-image FOTO [--apply]`; `myarch themes remove ID` quita uno.

Tu elección se guarda en `~/.local/state/myarch-wallpaper/`: cambiarla no
recarga nada. Leer los colores de una foto necesita ffmpeg (o ImageMagick).
