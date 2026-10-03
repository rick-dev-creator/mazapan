# Recordatorios

Una campana en la barra. Un clic: escribe qué recordar y elige cuándo — en
10 o 30 minutos, en una hora, esta noche, mañana por la mañana o a una hora
(17:30). Llegado el momento, se dice con una notificación. Uno que venció
con el equipo apagado o suspendido se dice al volver a encenderlo, con la
hora para la que era.

Opcional: está apagado hasta que lo enciendes (Ajustes › Plugins). Desde la
paleta, «Recordarme algo» abre la tarjeta; desde un script:

    qs ipc -c mazapan call reminders add "Llamar a Ana" 30

Se guardan en `~/.local/state/mazapan/reminders.json`, solo en este equipo.
