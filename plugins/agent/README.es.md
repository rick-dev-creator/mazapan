# Agentes

Tus agentes de programación en un solo sitio: Claude Code (cada una de tus
cuentas), opencode, pi, Codex y los demás, encontrados solos.

**En la barra**, un robot: naranja con un número cuando hay sesiones
esperándote, en el color de acento mientras uno trabaja, y el más lleno de
tus límites cuando pasa del 70 %. Un clic abre la tarjeta:

- *Sesiones*: cada una abierta, dónde y qué hace (trabajando, te espera,
  terminó), desde cuándo; un clic lleva a su ventana.
- *Límites*: las ventanas de 5 horas y semanal de cada cuenta de Claude (y
  las de un modelo concreto), cuánto están llenas y cuándo se reinician.
- *Hoy*: lo que usó cada agente: respuestas, tokens y lo que costarían
  esos tokens con los precios de API de cada modelo (y lo que se cobró a
  una clave de API).

Un aviso cuando una sesión te espera (un permiso, una pregunta), cuando
una termina tras trabajar un rato (`notify_after`, 30 s) y cuando una
cuenta pasa del `limit_alert` (80 %) de un límite.

**El panel** («Panel» en la tarjeta, o la paleta): coste o tokens por día
de cada agente, por modelo, por proyecto, cuándo trabajas (día × hora), el
uso de la caché y los límites de cada cuenta, en 7, 30 o 90 días, y lo que facturan los propios proveedores de API. En su
propia ventana, aparte de la barra.

**Claves de API en el llavero**, no en un `.env` en claro: «Agentes: una
clave de API» en la paleta (o `mazapan agents keys set openrouter`, la
clave se escribe oculta). `mazapan agents run opencode` (pi, aider…) abre
el agente con ellas como sus variables; Claude Code y Codex inician sesión
y no reciben ninguna. Una clave de OpenRouter, o una de administrador de
Anthropic u OpenAI, muestra además lo que facturan, hoy y en el periodo, y
el límite de la clave de OpenRouter.

**Lo que hizo Claude Code**, desde sus propias métricas de OpenTelemetry:
líneas escritas y quitadas (por repositorio), commits, pull requests, tu
tiempo y el del agente, en el panel. Claude Code las envía a un receptor
en este equipo (127.0.0.1, `otel_port`), que systemd arranca cuando llega
la primera y que se va cuando está inactivo; solo métricas, nunca un
prompt, guardadas en `~/.local/state/mazapan/agents`. Su settings.json
recibe las variables para ello (`otel`); si ya tienes tu propia
telemetría, se respeta.

**Un punto en el workspace** donde está la ventana de una sesión (con el
plugin de Workspaces): ámbar mientras te espera, el color del texto
mientras trabaja, verde cuando termina.

**Una pregunta desde la paleta**: escribe `?` y la pregunta (`? por qué
se gasta tanto la batería`). El primer agente que haya la responde en una
tarjeta, sin abrirlo: Claude Code con la cuenta que tenga margen y las
herramientas de solo lectura de Mazapán (puede mirar el estado de este
escritorio, nunca cambiarlo); si no, opencode, Codex, Gemini CLI o pi. La
respuesta se puede copiar, o seguir la conversación de Claude en una
terminal.

**Proyectos recientes en la paleta**: las carpetas en las que trabajaron
tus agentes últimamente (según sus propios registros) se encuentran
escribiendo su nombre; al elegir una, el proyecto se abre en tu editor
(`editor`: `code`, `rider`…) y una terminal en él continúa la última
conversación ahí, con el agente que se usó (Claude Code con la cuenta que
tenga margen, opencode, Codex, pi).

**Sobre esto**: una captura ("Preguntar" en la barra de la captura, o su
tecla), el texto seleccionado («Preguntar a un agente sobre el texto
seleccionado» en la paleta) o archivos (Archivos: clic derecho, Scripts,
Ask an agent) abren la misma tarjeta, para escribir la pregunta. Un texto
corto va dentro de la pregunta; una imagen u otro archivo, por su ruta,
que Claude puede leer (y nada más).

**Los cambios de un agente, tú los permites** (`approve_changes`):
cuando un agente cambia este escritorio por el servidor MCP de mazapan (un
tema, un ajuste, un plugin), antes aparece una tarjeta con quién lo pide y
el diff exacto de cada archivo; no se escribe nada hasta que pulsas
Permitir (sin respuesta en dos minutos, es que no). «Permitir siempre»
confía en ese agente desde entonces (`mazapan agents trust` los lista;
«Agentes: volver a preguntar antes de cada cambio» en la paleta). El Historial marca
cada cambio suyo con el nombre del agente, y «Deshacer todos los de ese
día» los revierte juntos.

**Preguntar a un agente sobre este escritorio**, desde la paleta, abre tu
agente (`command`; Claude con la primera cuenta que tenga margen) con el
informe de mazapan: estado, comprobaciones que fallan, cierres inesperados,
errores registrados. «Copiar un reporte» lo pone en el portapapeles. El
reporte va al agente que uses y, de ahí, adonde ese agente mande lo que
recibe: uno en la nube (Claude Code, Codex) lo envía a los servidores de su
empresa.

Cómo lo sabe: solo números, leídos en este equipo de lo que guarda cada
agente (los registros de proyectos de Claude Code, la base de datos de
opencode, las sesiones de pi y de Codex); nunca se leen prompts ni
respuestas. Los límites de Claude vienen de Anthropic con el propio inicio
de sesión de esa cuenta, que mazapan nunca renueva. Las sesiones las
cuentan los hooks de Claude Code, que este plugin pone en el
`settings.json` de cada configuración junto a los tuyos (`claude_hooks`; la
primera vez se guarda una copia del archivo; apagado los quita; al
desactivar el plugin entero se quedan, sin hacer daño: `mazapan agents
hooks remove` y `mazapan agents telemetry off` los quitan), y un pequeño plugin de opencode. Desde una terminal: `mazapan agents`, `mazapan
agents usage`, `mazapan agents limits`, `mazapan agents run claude`.
