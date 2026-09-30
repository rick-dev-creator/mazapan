# Apps web

Un sitio como una app propia: su propia ventana sin las barras del
navegador, su propio icono (el del sitio, que se descarga al añadirla),
en la paleta y el lanzador como cualquier app. Si la abres otra vez, la
ventana que ya está abierta pasa al frente en lugar de abrir otra.

"Apps web" en la paleta muestra las que tienes (un clic abre una, la
papelera la quita), añade una a partir de un nombre y una dirección, y
sugiere algunas (WhatsApp, Gmail, Calendar…) para añadir con un clic. No
se añade ninguna si no lo pides.

Se abren en tu navegador predeterminado si está basado en Chromium; si
no, en el primero instalado (Chromium, Chrome, Brave, Vivaldi, Edge…);
Firefox no tiene ventanas de app. Las sesiones son las del navegador: si
iniciaste sesión allí, también aquí.

Cada una es tuya, no de Mazapán: `~/.local/share/applications/mazapan-webapp-*.desktop`
y su icono en `~/.local/share/mazapan-webapps/`; se quedan aunque desactives
el plugin. Desde una terminal: `sh ~/.local/share/mazapan/bin/webapp add
NOMBRE URL`, `remove ID`, `list`, `open ID`.
