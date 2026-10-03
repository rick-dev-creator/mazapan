# Pantalla de arranque en el tema

La pantalla mientras arranca la computadora, con los colores del tema, y con
el disco cifrado su contraseña pedida ahí mismo, en tu idioma (Plymouth).
Sin imágenes: se dibuja con los colores del tema, así cualquier tema la tiene.
Su texto es ASCII simple («Mazapan», un asterisco por letra): lo que lo dibuja
antes de abrir el disco no sabe más que eso.

Un tema nuevo llega a ella con el siguiente `mazapan apply --system`, que
reconstruye el initramfs una vez (ahí vive la pantalla de arranque).
