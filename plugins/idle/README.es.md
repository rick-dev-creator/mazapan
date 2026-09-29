# Inactividad

Lo que pasa tras un rato sin uso:
- Primero las pantallas se atenúan 15 segundos (`dim`); un movimiento y
  vuelven.
- Luego se bloquea (`lock`, 5 minutos).
- Las pantallas se apagan (`screens_off`, 6).
- Se suspende con batería (`suspend_on_battery`, 15), y enchufado o en un
  equipo de escritorio solo si lo pides (`suspend`, 0: nunca).

Se bloquea antes de dormir, sea como sea (`lock_on_sleep`: la tapa, una
tecla, la paleta), y las pantallas vuelven a encenderse después.

Si una app pide mantener la pantalla encendida, se le hace caso: un vídeo
en el navegador, una llamada, una presentación. Tú también puedes:
"Mantener despierto" en la paleta, y una taza sale en la barra mientras
nada se atenúa, bloquea ni suspende solo (aun así se bloquea antes de dormir). Un clic en la taza, o la misma
acción, y se desactiva; al reiniciar también.

Es hypridle (`~/.config/hypr/hypridle.conf`), que arranca con Hyprland.
Si lo desactivas, se detiene en el siguiente inicio de sesión.
