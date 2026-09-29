# Snapshots del sistema en el menú de arranque

Con `hw-snapshots` activado y GRUB como gestor de arranque: los snapshots
en el menú de arranque ("Arch Linux snapshots"), al día gracias a
grub-btrfsd. Si una actualización deja el sistema sin arrancar: elige el
snapshot de antes y todo el escritorio arranca como estaba. Funciona
sobre una capa en memoria: lo que hagas ahí se pierde al reiniciar; es
para volver a ponerte en marcha (y deshacer lo que se rompió), no para
quedarte. Los snapshots tomados antes de activar esto arrancan solo en
parte: su initramfs no tiene la capa.

Solo se ofrece donde `/boot` está en el sistema de archivos raíz, para
que cada snapshot tenga su propio kernel: con `/boot` en una partición
propia (la EFI, lo que hace archinstall por defecto), un snapshot
arrancaría con el kernel de hoy y sus propios módulos, más viejos.
Necesita mkinitcpio (no dracut); un drop-in tuyo en
`/etc/mkinitcpio.conf.d` que ponga `HOOKS=(…)` después de este le quita
la capa (`myarch doctor` no puede mirar dentro del initramfs).

Desactivado hasta que lo activas: `myarch plugins enable
hw-snapshots-grub && myarch apply --system`. Si lo desactivas,
grub-btrfsd se detiene y las entradas que ya están siguen arrancando;
quita grub-btrfs para sacarlas del menú. Limine: todavía no
(limine-snapper-sync no está en los repositorios de Arch).
