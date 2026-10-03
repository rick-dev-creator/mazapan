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
uso de la caché y los límites de cada cuenta, en 7, 30 o 90 días. En su
propia ventana, aparte de la barra.

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
primera vez se guarda una copia del archivo; apagado los quita), y un
pequeño plugin de opencode. Desde una terminal: `mazapan agents`, `mazapan
agents usage`, `mazapan agents limits`, `mazapan agents run claude`.
