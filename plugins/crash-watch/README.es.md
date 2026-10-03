# Vigilante de cierres

Cuando una app se cierra de forma inesperada —el propio escritorio
incluido— una notificación dice cuál y cómo, con **Preguntar a un agente**:
abre el agente del plugin agent en una terminal con el informe de mazapan,
con los cierres del último día en él.

Los cierres se leen de systemd-coredump (`coredumpctl`) desde el último
avisado, así que uno que tumbó el escritorio se avisa al volver a entrar. La
misma app cerrándose una y otra vez se avisa una vez cada diez minutos.

Opcional: está apagado hasta que lo enciendes (Ajustes › Plugins).
