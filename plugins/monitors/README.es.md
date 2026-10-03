# Monitores

Acomoda tus pantallas una vez, y quedan así cada vez que vuelvas a
conectar las mismas pantallas.

## El gestor

`SUPER + SHIFT + M`, «Pantallas» en la primera pantalla de la paleta, o el
icono en la barra (`bar`, activado; con más de una pantalla dice cuántas
hay). Tus pantallas se ven como imágenes en vivo que arrastras a su lugar:
se pegan unas a otras y nunca se enciman. Mientras está abierto, cada
pantalla muestra su nombre, para que sepas cuál es cuál. Un clic en una
para ajustarla: encendida o apagada, modo, escala, rotación, sincronía
adaptativa (VRR), color de 10 bits, espejo de otra pantalla y qué
espacios de trabajo viven en ella (`1-5` o `7,8`).

**Probar** aplica la disposición y vuelve atrás sola a los 15 segundos
salvo que elijas **Mantener**, así un modo que te deja con la pantalla negra
no te deja atrapado. Una pantalla que no admite sincronía adaptativa o
color de 10 bits se queda sin ellos, y el gestor lo dice. Al menos una
pantalla tiene que quedar encendida y no ser espejo.

## Perfiles

**Guardar perfil** guarda la disposición con un nombre, para exactamente
estas pantallas. Desde entonces se aplica sola al iniciar y cada vez que
las pantallas van y vienen. Las pantallas se reconocen por su EDID (marca,
modelo, número de serie), no por el puerto donde están conectadas, que
puede cambiar; dos idénticas se distinguen por su puerto.

Cuando las pantallas conectadas no coinciden con ningún perfil, una
notificación explica cómo acomodarlas (`notify_unknown`, activado), y lo
que un perfil había apagado vuelve a encenderse: al desconectar el equipo
de su base nunca te quedas sin pantalla.

Los perfiles se guardan en `~/.config/mazapan/monitors.json`; el gestor lo
escribe, y tú también puedes editarlo.
