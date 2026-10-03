# Menú de arranque en el tema

El menú de GRUB con los colores del tema (también las entradas de los
snapshots): un solo diseño desde la primera pantalla hasta el escritorio.
Un tema nuevo llega a él con el siguiente `mazapan apply --system`.

El tema de GRUB lo escribe junto a grub.cfg un script que corre
grub-mkconfig (`/etc/grub.d/mazapan_theme`); quitar el plugin quita los dos.
