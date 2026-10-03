# Tema de Chromium

Chromium, Chrome, Brave y Edge con el tema, en cada perfil: cada uno queda
en su modo GTK, así su marco, pestañas, barra de herramientas y fuente
salen del tema de GTK (plugin Tema de GTK). Otras apps hechas con Chromium
conservan su propio aspecto.

Ese ajuste vive en el archivo Preferences de cada perfil, que el navegador
reescribe todo el tiempo: solo esa clave es de Mazapán. Mientras el
navegador está abierto su archivo no se toca (volvería a escribir su
propia copia), y se ajusta la vez siguiente, con el navegador cerrado.

El color de acento sigue siendo el del navegador: solo una política del
sistema puede fijarlo, y una política que fija un color de tema
(BrowserThemeColor) gana sobre el modo GTK.
