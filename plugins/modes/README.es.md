# Modos

Todo el escritorio cambia a la vez, como hace Focus en macOS con las
notificaciones (`SUPER + ALT + M`, o "Modos" en la paleta). Un modo
puede:
- estar en silencio (No molestar) y aun así dejar pasar algunas apps
  (Slack sí, lo demás no);
- cambiar el tema (y con él el fondo, si es el del tema);
- activar o desactivar la luz nocturna, y elegir el perfil de energía;
- mantener el equipo despierto (aun así se bloquea antes de dormir);
- ocultar widgets de la barra (el ticker de mercados durante una charla).

Cuatro para empezar, que puedes cambiar o eliminar: Trabajo,
Presentación, Noche y Juego. Cada uno se activa a mano (el panel, la
paleta, la barra), con horario (días y horas; puede pasar de medianoche)
o mientras haya una segunda pantalla conectada. Uno a la vez; al
desactivarlo, lo que cambió vuelve a como estaba (salvo que lo hayas
cambiado tú mientras tanto: un tema que elegiste durante el modo se
queda).

Los automáticos se desactivan cuando termina su horario. Si desactivas
uno a mano, espera a la próxima vez; uno activado a mano sigue hasta que
lo desactives. Mientras uno está activo, la barra lo muestra; un clic
cambia de modo o lo desactiva.

Los modos se guardan en `~/.local/state/myarch-modes/modes.json`. Un modo
hace lo que pueden hacer los plugins que tengas: el silencio necesita
`notifications`, la luz nocturna `night-light`, mantener despierto
`idle`, el perfil de energía power-profiles-daemon.
