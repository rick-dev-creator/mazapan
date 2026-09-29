# Apps

Instala apps según para qué usarás el equipo, como preguntan Windows y
macOS: tarjetas de Desarrollo, Juegos, Creatividad, Oficina, Streaming,
Trading… (`SUPER + ALT + A`, o "Apps" en la paleta). Un botón instala un
perfil completo, o elige varios (Desarrollo + Juegos + Oficina) e
instálalos juntos; un clic en la tarjeta muestra sus apps, cada una con
su casilla, para quien quiera elegir. O todas las apps por tipo, con
búsqueda; las instaladas tienen Abrir y Quitar.

Antes de empezar, lo que hará en una línea (6 apps, 546 MB de descarga,
1,8 GB en disco), cada paquete a un clic. La contraseña por el diálogo de
polkit, el progreso aquí, sin terminal. Las apps vienen de los
repositorios oficiales de Arch, o de Flathub para lo que no tienen
(Steam, Heroic), o son sitios como apps (apps web); un plugin de myarch
(Mercados) se añade desde el panel de Plugins, que primero muestra lo que
puede hacer. Sin AUR.

Cada instalación y desinstalación queda en el Historial, con su deshacer.
Quitar solo saca lo que el catálogo puso, nunca lo que otra cosa
necesita. Desde una terminal: `myarch apps`, `myarch apps install ID…`
(el id de una app o de un perfil), `myarch apps remove ID…`.

El catálogo es `catalog/apps.toml` en el repositorio de myarch: datos, no
código; añade una app o un perfil con un pull request.
