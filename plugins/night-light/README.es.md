# Luz nocturna

Las pantallas más cálidas de noche (`temperature`, 4000 K), con una
transición de media hora tras el anochecer y antes del amanecer
(`fade`), no de golpe.

Cuándo (`schedule`): en el horario que indiques (`hours`: `from` 20:00,
`to` 07:00), del atardecer al amanecer donde estés (`sun`, en `sun_at` =
"latitud,longitud"; se calcula aquí, no se consulta nada), o solo a mano
(`off`).

"Luz nocturna sí o no" en la paleta la cambia ya, y se queda así hasta
que el horario coincida: encendida por la tarde, sigue toda la noche;
apagada esta noche, vuelve mañana por la noche.

El tinte lo pone hyprsunset, que arranca con el shell (y se va con él: si
el shell se cae, las pantallas vuelven a la normalidad). Es dueño de
`~/.config/hypr/hyprsunset.conf`, que queda vacío: sus perfiles cambiarían
de golpe y chocarían con la transición.
