# Notificaciones

Las notificaciones de todas las apps, con el tema.

- **Avisos** que entran arriba a la derecha de la pantalla activa, de tres
  en tres (el resto espera su turno; todos quedan en el centro). El puntero encima detiene su
  tiempo; entonces se ven sus acciones, y Responder en las apps que lo
  admiten. Uno urgente lleva el borde rojo y se queda hasta que lo cierres.
  Un clic hace lo primero que ofrece la app, o trae su ventana; un clic
  derecho lo retira.
- **El centro** (`SUPER + N`, o la campana de la barra): solo las
  notificaciones, apiladas por app (un clic abre la pila), con su hora,
  acciones y respuestas, y Borrar todo. Por app (⋯): avisos o solo el
  centro; permitida con No molestar o no.
- **No molestar** (`SUPER + SHIFT + N`, clic derecho en la campana): a
  mano, cada día entre dos horas (`quiet_from`, `quiet_to`) y mientras una
  ventana está en pantalla completa. Lo que llega mientras tanto espera en
  el centro, y un aviso dice cuánto al terminar. Las urgentes se muestran
  igualmente (`quiet_critical`).
- La campana solo está en la barra si hay algo sin leer o No molestar está
  activado.

Es el servidor de notificaciones del escritorio
(`org.freedesktop.Notifications`): otro en marcha o instalado (mako,
dunst…) ocuparía su lugar; `myarch doctor` lo avisa. Los avisos esperan
mientras se hace una captura o una grabación. Las apps se reconocen por el
nombre que dan: una regla para una (No molestar) vale para lo que use su
nombre. Las notificaciones se guardan en
`~/.local/state/myarch-notifications/`. Lo que dicen se muestra como texto:
no se abren enlaces ni se descargan imágenes de ningún sitio.
