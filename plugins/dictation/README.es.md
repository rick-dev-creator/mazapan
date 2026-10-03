# Dictado

Habla en vez de escribir. `SUPER + CTRL + D` empieza a escuchar; la misma
tecla otra vez para, y lo que dijiste se escribe en la ventana donde estás
(o se copia, para pegarlo, con `type` apagado). A los dos minutos para
solo.

Se procesa en este equipo, con whisper.cpp: nada de lo que dices sale de
él. La primera vez, el modelo con el que entiende se descarga una sola vez
desde whisper.cpp (Hugging Face) y se comprueba contra su suma conocida:
`base` (148 MB) por defecto, `tiny` (75 MB) para equipos antiguos, `small`
(488 MB) para entender mejor. Se guarda en `~/.local/share/mazapan/whisper`.

Escucha en el idioma del escritorio, o en el que pongas en `language`
("auto" lo adivina cada vez). Mientras escucha, el punto naranja de la
barra dice que el micrófono está en uso.

Opcional: está apagado hasta que lo enciendes (Ajustes › Plugins).
