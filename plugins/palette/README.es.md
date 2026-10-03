# Paleta de comandos

Un solo lugar para todo: apps, ventanas abiertas, y las acciones y atajos
de cada plugin. `SUPER + Space`, o el botón al principio de la barra
(`bar`, activado).

## La primera pantalla

Abierta sin escribir nada, es el menú del escritorio: un mosaico para cada
lugar (Apps, Actualizaciones, Ajustes, Pantallas, Captura…;
Actualizaciones dice cuántas hay pendientes), la energía del equipo (un
segundo ↵ confirma apagar, reiniciar y similares) y debajo las ventanas
abiertas. Las flechas se mueven entre ellos.

## Buscar

Escribe para encontrar apps, ventanas (por título, app o espacio de
trabajo), acciones y atajos. Las letras no tienen que ir juntas: «vsc»
encuentra Visual Studio Code. También aparecen las apps que todavía no
están instaladas, como «Instalar …», que las abre en el panel de Apps.
Empieza con `> ` para buscar solo acciones y atajos.

↑ ↓ (o Tab) eligen, ↵ ejecuta, Ctrl+C copia el comando, Esc cierra. Un
atajo que es solo una tecla dice qué teclas pulsar.

Cada resultado muestra el comando que ejecuta (`show_commands`,
activado): empiezas haciendo clic y terminas sabiéndote el comando.

## Sus propias acciones

«Actualizar el sistema», «Comprobar que todo funciona», «Historial de
actualizaciones», «Deshacer la última actualización» y «Volver a aplicar
la configuración»: `mazapan update`, `doctor`, `history`, `rollback` y
`apply`, cada uno en una terminal que queda abierta para leerla
(`terminal_hold`, `foot --hold`). Las apps de terminal como btop se abren
en `terminal` (`foot`).
