# Captura

Una sola forma de tomar la pantalla (`Print`, «Captura» en la primera
pantalla de la paleta). Todas las pantallas se congelan bajo un velo
oscuro: arrastra una región, o haz clic en una ventana, o en la pantalla
vacía para tomarla entera. Luego:

- **Copiar** (`c`) pone la imagen en el portapapeles.
- **Guardar** (`g`) la guarda en `~/Pictures/Screenshots` (`pictures`),
  como «Screenshot» con la fecha y la hora.
- **Texto** (`t`) lee el texto que hay en ella (OCR) y lo copia.
- **Código QR** (`q`) copia lo que contiene un código QR en ella, como
  secreto: nunca se muestra y el historial del portapapeles no lo guarda
  (puede ser una contraseña o la clave de un Wi‑Fi).
- **Anotar** (`a`): lápiz, flecha, recuadro o marcador, en unos cuantos
  colores; Deshacer (o Ctrl+Z) quita el último trazo, Listo (o ↵) vuelve a
  las acciones. Lo que dibujas va en lo que copias o guardas.
- **Grabar** (`r`) graba esa región como video. **Grabar con cámara**
  (si hay una) pone antes tu cámara en una ventanita en la esquina inferior
  derecha de la pantalla, en todos los workspaces, y la quita al terminar:
  elige una región que incluya esa esquina (o toda la pantalla).

↵ copia y guarda a la vez; Esc sale. Las teclas en inglés también sirven
(`s` para guardar). Al señalar una acción se ve el comando que ejecuta.
Una notificación dice qué pasó (con la imagen, cuando se guardó).

Mientras graba, un punto rojo y el tiempo quedan en el centro de la barra.
Un clic en él, `Print` otra vez o «Detener la grabación de pantalla» en la
paleta la detienen, y se guarda en `~/Videos/Recordings` (`videos`). El
sonido no se graba salvo que lo pidas (`audio`: la salida por defecto).
Las notificaciones se retienen mientras se toma una imagen o se graba un
video, para que nunca salgan en ellos.

El texto se lee en el idioma del sistema y en inglés, los que tengan
instalados sus datos de Tesseract (`tesseract-data-spa`, `-fra`…; el
instalador añade los del idioma si hay conexión). Las
pantallas congeladas quedan en una carpeta privada y se borran en cuanto
dejan de hacer falta. Con grim, wl-clipboard, Tesseract, zbar y wf-recorder.
