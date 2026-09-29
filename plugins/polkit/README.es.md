# Contraseñas del sistema

Cuando una app necesita permisos que no tiene (montar un disco, cambiar la
hora, instalar algo), polkit pide una contraseña. Esto es lo que la pide:
una tarjeta sobre el escritorio atenuado, con el tema, que dice qué se pide
(con las palabras de la app) y qué acción es. Una contraseña incorrecta la
sacude y deja volver a intentarlo; Esc o Cancelar lo rechaza. Con varios
administradores, eliges de quién es la contraseña. Los mensajes de un
lector de huellas también salen ahí.

Sin un agente, esas peticiones fallan sin decir nada. Polkit admite un
agente por sesión: si otro (el de GNOME, el de KDE, hyprpolkitagent) llegó
antes, `myarch doctor` lo avisa.

La contraseña va a polkit y a ningún otro sitio: el campo se vacía en
cuanto se envía.
