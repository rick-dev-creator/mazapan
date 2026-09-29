# Historial del portapapeles

Lo que copiaste, guardado: texto e imágenes, lo más reciente primero, con
búsqueda (`SUPER + CTRL + V`). ↵ (o un clic) lo vuelve a poner y lo pega
donde estabas (`paste`, con wtype); clic derecho o Supr borra uno; Ctrl+P
fija uno (se queda aunque vengan más, y arriba); Borrar todo respeta los
fijados.

Lo mismo copiado otra vez sube en vez de salir dos veces, y una copia es
una entrada (una imagen copiada en el navegador no se guarda también como
su HTML). Lo marcado como secreto nunca se guarda: KeePassXC lo marca (el
tipo x-kde-passwordManagerHint), y también `wl-copy --sensitive`; no todos
los gestores de contraseñas lo hacen, así que revisa el tuyo. Tampoco las
copias más grandes que `max_kb`. El historial está en
`~/.local/state/myarch-clipboard/`, legible solo por ti; como mucho `keep`
entradas (100), `keep_images` de ellas imágenes (20).
