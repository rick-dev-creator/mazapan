# Batería

En un portátil (nunca en un sobremesa, donde no hay batería): el nivel de
la batería en la barra (un rayo mientras carga, ámbar cuando se está
acabando, rojo cuando está casi vacía), y un clic para más: si carga y
hasta cuándo, o cuánto dura; su salud respecto a cuando era nueva; el
perfil de energía (ahorro, equilibrado, rendimiento, con
power-profiles-daemon); el brillo de la pantalla (con retroiluminación).

Una notificación cuando se está acabando (`low`, 15 %) y una urgente
cuando está casi vacía (`critical`, 5 %, nunca por encima de `low`): una
vez cada una hasta que se enchufa, con las pantallas (y recargas) que sean. Los niveles vienen de UPower; el brillo, de brightnessctl.
