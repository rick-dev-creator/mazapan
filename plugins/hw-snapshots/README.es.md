# Snapshots del sistema

Donde el sistema está en btrfs: un snapshot de `/` antes y después de
cada cambio de paquetes (una actualización, una instalación, una
desinstalación), con snap-pac y snapper. Se guardan los últimos `keep`
cambios (10). Aparecen en el Historial del escritorio con lo que cambió;
con `hw-snapshots-grub` también en el menú de arranque, para arrancar el
sistema como estaba.

Como todo plugin de hardware, está desactivado hasta que lo activas:
`myarch plugins enable hw-snapshots && myarch apply --system` (antes
lista lo que hará como root).

Mejor con `/home` en un subvolumen propio (lo que hace archinstall por
defecto): los snapshots son del sistema, no de tus archivos (si `/home`
está dentro de `/`, deshacer los archivos de un cambio de paquetes
devolvería también los tuyos). `/.snapshots` se deja listable, para que
el panel de Historial diga qué hay sin tener permisos sobre los
snapshots; cada archivo dentro conserva sus propios permisos.

Si lo desactivas, myarch deja de gestionarlo, pero snap-pac sigue
tomándolos hasta que se quita (`sudo pacman -R snap-pac`, o `myarch undo`
justo después de activarlo); los que hay se quedan
(`sudo snapper -c root list`).
