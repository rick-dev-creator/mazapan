# Espacios de trabajo

Los números de los espacios de trabajo en la barra: del 1 al 5 siempre
(`persistent`), cualquier otro mientras existe, y el 10 se muestra como 0
(su tecla). En el que estás es un bloque del color de acento que se desliza
de un número a otro; uno que se ve en otro monitor va subrayado; los vacíos
van atenuados. Un clic en un número va a ese espacio; la rueda encima los
recorre. Con el plugin de Agentes, un punto en un número indica que ahí
hay una sesión de un agente: ámbar mientras te espera, el color del texto
mientras trabaja, verde cuando termina.

Un clic derecho en un número abre una vista previa en vivo de ese espacio
(otra vez para cerrarla), cada ventana en su lugar y tamaño reales; crece
un momento después. Mientras está abierta, señalar otro número muestra ese.
Con `trigger` en `hover`, basta con señalar un número para abrirla
(después de `open_delay_ms`, 350 ms). No hay vista previa de lo que este
monitor ya muestra. Se desactiva con `preview`.

En la vista previa, un clic en una ventana va a ella. Sus ventanas se
pueden sacar arrastrándolas: soltada en la pantalla, una ventana viene al
espacio en el que estás y recibe el foco; soltada en otro número, se mueve
allí sin llevarte con ella; soltada otra vez en la tarjeta, no pasa nada.

Lo demás es tamaño: `cell` (28) y `font_size` (9.5 puntos) para los
números; `preview_width` (320) y `preview_width_large` (640) para la
tarjeta, pequeña y grande, tras `grow_delay_ms` (200 ms); `preview_aspect`
(1.6) la mantiene al menos así de apaisada, y un ultrapanorámico conserva
su propia forma.

## Vista general

`SUPER + Tab` (`overview_key`) muestra todos los espacios de trabajo de la
pantalla a la vez, como la vista general de Niri: cada uno con sus
ventanas en vivo, la tira de columnas entera (lo que está en pantalla,
marcado) y uno vacío después del último. Un clic en una ventana va a ella;
en un espacio, a ese espacio. Una ventana arrastrada a otro espacio va
allí. `1`…`9` van a ese espacio; Esc la cierra.
