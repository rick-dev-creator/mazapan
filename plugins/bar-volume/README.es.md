# Volumen

El volumen de la salida en la barra, como icono y porcentaje
(`show_percent`, activado). La rueda encima lo cambia (`step`, 5 % por
paso), un clic central lo silencia o le devuelve el sonido. Subirlo
también le quita el silencio.

Un clic abre la tarjeta: la salida y la entrada (el micrófono), cada una
con silencio y un deslizador y, cuando hay más de uno, los dispositivos
para elegir. El que eliges pasa a ser el predeterminado.

`max_volume` es lo más alto a lo que llegan el deslizador y la rueda: 1.0
es 100 %; hasta 1.5 sube las fuentes bajas (puede distorsionar).

Es PipeWire, a través de WirePlumber. Con tarjeta de sonido, «Comprobar
que todo funciona» se asegura de que haya una salida predeterminada.
