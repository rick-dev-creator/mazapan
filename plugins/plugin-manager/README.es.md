# Plugins

Donde buscas, instalas y configuras plugins, como la vista de
extensiones de un editor (`SUPER + SHIFT + P`, o Plugins en la primera
pantalla de la paleta). Todos los plugins que Mazapán conoce: los
incluidos, los tuyos, los de git y los de los catálogos. Pestañas para
los instalados, los disponibles, los de este equipo y todos; `/` busca.

Cada plugin tiene su página: su README (sin imágenes ni HTML), qué
puede hacer (lo arriesgado, marcado) y sus ajustes, que se cambian ahí
mismo. Instalar uno muestra primero qué puede hacer; "Permitir e
instalar" instala justo la versión que viste.

Activar y desactivar plugins y cambiar ajustes pasa por `mazapan apply`,
así que `mazapan undo` lo deshace. Lo que pide una contraseña o una
aprobación (las partes de un plugin de hardware que corren como root,
una actualización que puede hacer más que antes) se abre en una
terminal, y el panel vuelve cuando termina.

"Actualizar plugins de git", en la paleta, ejecuta `mazapan plugins
update` en una terminal.

Ajustes: `key` (`SUPER + SHIFT + P`) abre el panel; `terminal`
(`foot --hold`) es la terminal para lo que pide algo, que queda abierta
para leerla.
