# Teclas multimedia

Las teclas de volumen, micrófono, brillo y reproducción funcionan (también
con la pantalla bloqueada; mantenidas, se repiten), y lo que hicieron se ve
un momento: una tarjeta pequeña en la pantalla activa con el nivel, o la
canción. Subir el volumen quita el silencio, como en un móvil. La tarjeta
de volumen también sale cuando otra cosa lo cambia (la barra, una app). Los
clics atraviesan la tarjeta.

El volumen y el micrófono van por `wpctl` (PipeWire), el brillo por
`brightnessctl` (`brightness_step`), la reproducción por MPRIS (el
reproductor que suena, o si no el primero). `max_volume` por encima de 1.0
sube las fuentes bajas (puede distorsionar).
