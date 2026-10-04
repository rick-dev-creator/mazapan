# Juegos

Se activa con las apps de Juegos (Steam, Lutris, Heroic):

- **En una laptop con gráficos híbridos** (una NVIDIA junto a la GPU
  integrada), Steam, Lutris y Heroic arrancan en la NVIDIA, y sus juegos
  con ellos: lanzadores propios con `prime-run`, rehechos cada vez que esas
  apps se instalan o actualizan (`discrete_gpu`). Todo lo demás sigue en la
  integrada, que ahorra batería.
- **Los drivers de 32 bits** para las GPU de este equipo
  (`lib32-vulkan-radeon`, `lib32-vulkan-intel`, `lib32-nvidia-utils`):
  muchos juegos de Windows por Proton y Wine son de 32 bits. AMD, Intel y
  NVIDIA por igual.
- **Menor retraso:** los juegos dibujan en cuanto están listos, con tearing
  permitido solo para ellos (`low_latency`); el VRR (FreeSync, G-Sync) va
  por pantalla, en Monitores.
- **La pantalla nunca se atenúa ni se bloquea** mientras un juego está al
  frente.
- Con **Modos**, el modo Juego (silencio, máxima potencia, despierto) se
  activa mientras hay un juego abierto y se quita al cerrarlo.
- GameMode.

El anticheat de algunos juegos no admite Linux (Valorant, Fortnite, algunos
de EA y Riot): ninguna distribución los ejecuta. protondb.com dice cómo
funciona cada uno.
