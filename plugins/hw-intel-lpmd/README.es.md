# Modo de bajo consumo de Intel

El Low Power Mode Daemon de Intel (`intel_lpmd`), para portátiles con
procesador Intel híbrido: Alder Lake, Raptor Lake, Meteor Lake, Lunar
Lake y Panther Lake. Cuando el equipo hace poco, deja ese trabajo en los
núcleos eficientes y los demás descansan, y la batería dura más.

Se ofrece en portátiles con uno de esos procesadores, que se reconocen
por sus gráficos integrados (Alder Lake-P, -U y -HX, Raptor Lake-P y -U,
Meteor Lake, Lunar Lake, Panther Lake). No en Alder Lake-N, que no es
híbrido. Un mini PC con uno de estos procesadores de portátil también lo
ve (ahí no hace daño; es para la batería).

Lo que hace, como root: instala `intel-lpmd` y arranca
`intel_lpmd.service` ya y en cada arranque. El archivo que dice que está
activado es `/etc/tmpfiles.d/mazapan-intel-lpmd.conf`; sus propios
ajustes están en `/etc/intel_lpmd/`. Al desactivarlo, el servicio se
para y deja de arrancar. Sin ajustes.

Como todo plugin de hardware, está desactivado hasta que lo activas:
"Activar" aquí, o `mazapan plugins enable hw-intel-lpmd && mazapan
apply --system`. Lo que corre como root se lista antes, y pide tu
contraseña en una terminal; `mazapan undo` lo deshace.
