# Instalador

El instalador del ISO: este sistema en un disco, en pocas pantallas. El
idioma (el escritorio live cambia al instante), el teclado (lo pruebas
ahí mismo), dónde estás, el disco (entero: solo eliges cuál, y se dice
qué tiene ahora), tu cuenta (nombre, usuario, contraseña, el nombre del
equipo, a partir de tu nombre) y para qué usarás el equipo: perfiles,
varios a la vez, cada app para ver y cambiar para quien quiera. Una
revisión, y `mazapan install run` lo hace con archinstall, desde el
repositorio del propio ISO: no se descarga nada.

Solo en el sistema live del ISO (`[hardware] live = true`). En el primer
inicio, la bienvenida sigue donde se quedó, y las apps elegidas aquí se
instalan en cuanto hay conexión.

Sin cifrado de disco, `/boot` está en btrfs (los snapshots arrancan con
su propio kernel) y la pantalla de inicio de sesión es la del plugin
`login`; con cifrado, la contraseña del disco es el inicio de sesión.
