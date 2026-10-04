# Centro de control

Un panel para lo que antes era una fila de iconos a la derecha de la barra,
que crece desde una **pastilla de estado**:

- **La pastilla** muestra la red, el volumen, la batería y cuántas
  notificaciones hay mientras nada te necesita; cuando algo sí, dice qué, en
  su color: "Claude · shop-api espera", "Grabando 0:42", "Actualización lista".
- **Ahora**: solo lo que te necesita, cada cosa con su acción ahí mismo (ir a
  la ventana del agente, detener la grabación, actualizar).
- **Interruptores**, dos por fila: Wi-Fi (sus redes se abren ahí mismo),
  Bluetooth (sus dispositivos), No molestar, luz nocturna, mantener
  despierto, ahorro de energía.
- **Secciones**: el sonido y a dónde va (bocinas, audífonos, HDMI), la música
  que suena, tus agentes y sus límites, las notificaciones.

`SUPER + A` lo abre, o un clic en la pastilla; Escape o un clic afuera lo
cierra. Sigue el tema, como todo.

Cada parte viene del plugin al que pertenece, como los widgets de la barra:
un plugin pone archivos en `control/now/`, `control/tiles/` o
`control/sections/`, y su widget de la barra con el mismo nombre sale de la
barra mientras el Centro de control está encendido. Apágalo y la barra queda
como estaba.
