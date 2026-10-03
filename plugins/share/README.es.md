# Compartir

Archivos, o lo que copiaste (una imagen, archivos copiados en Archivos,
texto), a otro dispositivo: uno cercano con LocalSend, o uno de los tuyos
en tu tailnet con Taildrop de Tailscale. Solo se ofrecen las vías que
tiene este equipo: instala LocalSend o Tailscale desde Apps.

Cómo compartir:
- **Lo que copiaste**: «Compartir lo que copié» en la paleta. Una imagen
  o un texto se convierte antes en un archivo (en la carpeta de ejecución,
  solo tuya, que se borra al cerrar sesión).
- **Archivos**: en Archivos, selecciónalos, clic derecho, Scripts, Share.
  O desde una terminal: `~/.local/share/mazapan/bin/share ARCHIVO…`.

El panel Compartir muestra a dónde pueden ir. **Un dispositivo cercano**
abre LocalSend con los archivos, para elegir el dispositivo en su ventana
(encuentra móviles y ordenadores de la misma red). **Tu tailnet** muestra
tus dispositivos conectados; un clic envía al momento (Taildrop) y una
notificación avisa cuando llega. Enviar con Taildrop sin root requiere que
seas el operador de Tailscale: el plugin de Tailscale lo hace.

Recibir: la propia ventana de LocalSend recibe; con Taildrop, el plugin de
Tailscale deja lo que llega en Descargas y avisa.

Lo que escribe: el panel (`~/.config/quickshell/mazapan/panels/share.qml`),
`~/.local/share/mazapan/bin/share` y el script de Archivos
(`~/.local/share/nautilus/scripts/Share`). Sin ajustes.
