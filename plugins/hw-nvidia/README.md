# NVIDIA (Turing and newer)

NVIDIA's open driver, for GeForce RTX 20 and newer. Offered where the
GPU is an NVIDIA one from Turing on: those run NVIDIA's open kernel
modules.

What it does, as root:
- Installs `nvidia-open-dkms`, `nvidia-utils`, `libva-nvidia-driver`
  and `linux-headers`. The driver is built for your kernel on install,
  so every installed kernel needs its headers; a check looks for them
  before the files below are written.
- Two kernel files: modesetting on (with the console on the NVIDIA
  driver too), in `/etc/modprobe.d/mazapan-nvidia.conf`, and the NVIDIA
  modules in the initramfs, in `/etc/mkinitcpio.conf.d/mazapan-nvidia.conf`,
  so the driver starts before the screen does. The initramfs is rebuilt,
  and it takes effect after a reboot.

And Hyprland's variables, in `~/.config/hypr/mazapan/hw-nvidia.lua`.
Where the NVIDIA GPU is the only one, video decoding and GLX go through
its driver. With several GPUs nothing is set: apps stay on the one that
drives the screens, so NVIDIA's doesn't stay awake for everything and
video decoding keeps working on the other. No settings.

Like any hardware plugin it's off until you turn it on: "Turn on" here,
or `mazapan plugins enable hw-nvidia && mazapan apply --system`. What
runs as root is listed first, and asks for your password in a terminal;
`mazapan undo` takes it back.
