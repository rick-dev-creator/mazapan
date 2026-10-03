# Indicadores de privacidad

Un punto en la barra mientras algo escucha o mira, como en macOS: naranja
para el micrófono, verde para la cámara, azul para la pantalla compartida.
Con un clic dice qué app. No se ve nada mientras nada esté en uso.

Lo que está en uso se lee de PipeWire (sus flujos y fuentes en marcha, como
el módulo de privacidad de Waybar), y las cámaras abiertas por fuera de
PipeWire (algunos navegadores) también cuentan.

Una app que quiere ver la pantalla pregunta antes, como en macOS: Hyprland
muestra quién lo pide, y tú niegas, permites una vez o permites y lo
recuerda. Las herramientas del propio escritorio (captura, grabación, el
selector de color, las vistas previas de la barra, la pantalla de
bloqueo) no preguntan, ni compartir pantalla por el portal, que muestra
su propio selector. `screen_ask` (encendido) lo apaga; un cambio vale
desde el próximo inicio de sesión.

Lo que eso frena, dicho claro: las apps en un sandbox (Flatpak). Un
programa que corre como tú fuera de uno puede usar las mismas herramientas
que no preguntan (grim, por ejemplo), como en cualquier escritorio Linux;
el diálogo es para las apps, no un muro contra lo que tú mismo ejecutas.
