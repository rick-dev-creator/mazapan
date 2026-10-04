# Puntos de restauración

Antes de cada cambio en los paquetes del sistema (una actualización, una
instalación, una desinstalación) snapper toma un snapshot del sistema
(`hw-snapshots`). Este plugin los convierte en **puntos de restauración**
que cualquiera puede usar:

- **En el menú de arranque**, en *Checkpoints*, cada uno con lo que había
  antes: "Oct 4, 14:02: before an update (12 packages)". Los cinco más
  recientes (`entries`). También con el disco cifrado: ahí `/boot` es la
  partición EFI, así que el kernel y el initramfs de cada punto se guardan
  en ella (`/boot/mazapan/k`, por contenido: los mismos se guardan una vez).
- **Si arrancas desde uno, lo dice**: una tarjeta al iniciar el escritorio
  y una marca en la barra mientras dure. Un punto corre sobre una capa en
  memoria: lo que cambies ahí se pierde al reiniciar (tu carpeta personal
  es la de siempre). Tres respuestas:
  - **Quedarme con este**: pasa a ser tu sistema. El que sustituye se
    guarda `keep_days` días (7) como "el sistema anterior", también en el
    menú de arranque, y después se borra.
  - **Volver a mi sistema**: reiniciar.
  - **¿Qué se rompió?**: un agente recibe lo que difiere entre el punto y
    el sistema principal (paquetes, la última actualización de Mazapán, los
    errores del arranque que falló), solo para leer, y dice qué se rompió y
    cómo arreglarlo.
- **Desde el Historial**, con el sistema funcionando: "Restaurar este
  punto" (desde el próximo arranque), sin pasar por el menú de arranque.

Desde una terminal: `mazapan checkpoint` (dónde estás), `list`,
`diagnose [N]`, `sudo mazapan checkpoint keep`, `sudo mazapan checkpoint
restore N`. Los agentes (MCP) leen `checkpoints` y `checkpoint_diagnose`;
quedarse con uno o restaurarlo lo decides tú.

Necesita el esquema de Mazapán, el que hace su instalador: btrfs con el
sistema en un subvolumen propio (`@`, con los snapshots de snapper dentro),
GRUB y mkinitcpio con el initramfs de systemd. Con otro esquema `mazapan
checkpoint` lo dice y no toca el menú.
