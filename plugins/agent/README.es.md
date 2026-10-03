# Agente

Pregunta a un agente de programación sobre este escritorio, desde la
paleta: «Preguntar a un agente sobre este escritorio» lo abre en una
terminal, con un reporte del escritorio como primer mensaje. Luego
pregúntale qué falla, o cómo cambiar algo.

El reporte es `mazapan report`: el estado del escritorio (tema, idioma,
plugins), las comprobaciones que fallan y su salida, los archivos
generados que difieren, la última actualización, los cierres inesperados
recientes y los errores que registraron Hyprland y la shell. También le
dice al agente que cambie las cosas con mazapan (vista previa, aplicar,
deshacer), nunca editando los archivos generados.

El reporte va al agente que uses y, de ahí, adonde ese agente mande lo que
recibe: uno en la nube (Claude Code, Codex) lo envía a los servidores de su
empresa. No lleva contraseñas ni claves, pero sí nombra tus apps, los
archivos que difieren y lo que se registró; «Copiar un reporte» te deja
leerlo antes.

«Copiar un reporte de lo que falla» pone el mismo reporte en el
portapapeles: para leerlo antes, o pegarlo donde alguien te esté
ayudando.

El agente es `command` (`claude`, Claude Code). Sirve cualquier agente que
acepte una petición como argumento (`codex`, `opencode run`…). Para que use
mazapan directamente (estado, vistas previas, aplicar, deshacer), registra
con él el servidor MCP de mazapan:

    claude mcp add --scope user mazapan -- mazapan mcp
