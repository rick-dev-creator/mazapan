# Tiempo

El tiempo de ahora en el centro de la barra: un icono y la temperatura. Un
clic abre los detalles: cómo está el cielo, la sensación térmica, la
máxima y la mínima del día, la humedad, el viento, la probabilidad de
lluvia, el amanecer y el atardecer; luego las próximas horas y los
próximos días. Un clic fuera, Esc o el widget otra vez los cierran.

Dónde (`location`): `auto` te localiza por tu IP pública, que le pregunta
a ipinfo.io; un nombre de ciudad se busca con la geocodificación de
Open-Meteo; o da coordenadas como «lat,lon», y no se pregunta nada más que
el pronóstico. El lugar se busca una vez; el pronóstico viene de
Open-Meteo, sin cuenta ni clave, cada 15 minutos (`refresh_minutes`), una
sola vez para todas las barras.

`units`: `metric` (°C, km/h) o `imperial` (°F, mph). Los nombres de los
días y las horas van en el idioma del sistema.
