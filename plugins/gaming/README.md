# Gaming

Turned on with the Gaming apps (Steam, Lutris, Heroic):

- **On a laptop with hybrid graphics** (an NVIDIA card beside the
  integrated GPU), Steam, Lutris and Heroic start on the NVIDIA card, and
  their games with them: launchers of their own through `prime-run`, made
  again whenever those apps are installed or updated (`discrete_gpu`).
  Everything else stays on the integrated GPU, which saves the battery.
- **The 32-bit drivers** for this computer's GPUs (`lib32-vulkan-radeon`,
  `lib32-vulkan-intel`, `lib32-nvidia-utils`): many Windows games through
  Proton and Wine are 32-bit. AMD, Intel and NVIDIA alike.
- **Least delay:** games draw the moment they're ready, tearing allowed for
  them alone (`low_latency`); VRR (FreeSync, G-Sync) is per screen, in
  Monitors.
- **The screen never dims or locks** while a game is the window in front.
- With **Modes**, the Game mode (quiet, full power, awake) comes on while a
  game is open and goes when it closes.
- GameMode.

Some games' anti-cheat doesn't allow Linux at all (Valorant, Fortnite,
some of EA's and Riot's): no distribution runs them. protondb.com says
how each one runs.
