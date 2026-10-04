# Lector de huellas

Un dedo, además de la contraseña:

- **desbloquea la pantalla** (hyprlock se lo pide a fprintd);
- **permite cambios del sistema**: las peticiones de contraseña de Mazapán
  (polkit) aceptan primero el dedo, y la contraseña sigue sirviendo;
- con la tapa cerrada (el lector queda debajo) se pide la contraseña
  directamente.

«Huella: añadir un dedo» en la paleta registra uno (toca el lector unas
cuantas veces); «Huella: quitar tus dedos» los olvida. `sudo` en una
terminal sigue pidiendo la contraseña: su archivo PAM es del propio
paquete sudo, que Mazapán nunca edita.

Lo que hace como root: instala `fprintd` y escribe `/etc/pam.d/polkit-1`,
el servicio propio de polkit (que está en `/usr/lib/pam.d`) con el dedo
primero; al desactivarlo, el archivo se va y vuelve a contar el de polkit.

Se ofrece en equipos con un lector que fprintd soporta (Goodix, Synaptics,
Elan, Validity, Egis, FocalTech, FPC…); el instalador lo activa ahí.
