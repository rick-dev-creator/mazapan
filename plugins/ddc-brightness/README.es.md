# Brillo de las pantallas externas

Las teclas de brillo cambian también las pantallas externas, por el cable
(DDC/CI), igual que la del portátil; en un sobremesa sin pantalla propia,
cambian las externas y muestran su nivel. Más brillo y Menos brillo de la
paleta hacen lo mismo. La mayoría de pantallas responden; algunas tienen
DDC/CI apagado en su propio menú.

Instala `ddcutil`, que trae su propia regla udev (tu sesión puede llegar a
los buses I2C de las pantallas) y carga `i2c-dev` en cada arranque: cuenta
desde el próximo arranque. Las pantallas se buscan una vez cada pocos
minutos (tarda un segundo o dos), y una tecla mantenida las cambia paso a
paso.
