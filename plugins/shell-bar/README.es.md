# Barra

Una barra arriba en cada pantalla (Quickshell), que arranca con
Hyprland. No tiene nada propio: el reloj, los workspaces, la batería y
lo demás son widgets que otros plugins ponen a su izquierda, centro o
derecha. Cada widget se carga por su cuenta: uno que falla muestra un
aviso en su lugar, y los demás siguen funcionando.

El centro se queda centrado mientras hay espacio. En una pantalla
estrecha se mueve a la izquierda, sin tocar la derecha, y la izquierda
cede espacio: primero lo que puede encogerse (el título de una
ventana), y luego se corta.

Los paneles que abren otros plugins (el de plugins, el de monitores…)
corren en el mismo shell.

Una comprobación se asegura de que la barra corre y carga su
configuración. Tras una actualización de Quickshell, antes reinicia la
barra con la versión nueva.

`height` (28): la altura de la barra en píxeles lógicos, de 20 a 64.

## Para quien escribe plugins

Un widget es un archivo QML en `~/.config/quickshell/mazapan/widgets/left`,
`center` o `right`, colocado por orden de nombre de archivo (un prefijo
como `10-` elige su lugar). La barra trae un kit de componentes para
ellos (un elemento de barra, su tarjeta emergente, etiquetas,
interruptores, deslizadores, filas de lista, botones…) en
`components/kit`. Ver `docs/plugin-api.md`.
