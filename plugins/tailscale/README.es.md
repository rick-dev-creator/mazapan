# Tailscale

Tailscale listo para usar, como lo deja Omarchy y un poco más:

- su servicio, `tailscaled`, en marcha ya y en cada arranque;
- tú como su operador, así que iniciar sesión (`tailscale up`) y enviar o
  recibir archivos con Taildrop no necesitan sudo (se mantiene cada vez
  que arranca el servicio);
- «Tailscale: iniciar sesión» en la paleta muestra la dirección para
  entrar; «Tailscale: dispositivos y estado» lista tu tailnet;
- los archivos que te envíen tus otros dispositivos con Taildrop llegan a
  Descargas (si el nombre ya existe, se renombra), cada uno con un aviso
  para abrirlo o abrir su carpeta (`receive`).

Enviar es cosa del plugin Compartir: tus dispositivos conectados aparecen
en su panel.

Lo que hace como root: instala `tailscale` y escribe
`/etc/systemd/system/tailscaled.service.d/mazapan.conf` (el operador, que
se fija cuando el servicio arranca), y activa y reinicia `tailscaled`. Al
desactivarlo, el servicio se para y deja de arrancar. Como tú: el servicio
que recibe (`~/.config/systemd/user/mazapan-taildrop.service`) y su
script.

Está desactivado hasta que lo activas: instalar Tailscale en Apps lo
activa, o `mazapan plugins enable tailscale && mazapan apply --system`.
