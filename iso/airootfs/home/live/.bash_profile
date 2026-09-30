# The live session: mazapan writes the desktop, then Hyprland starts. Not
# exec'd: if Hyprland dies there's a shell on tty1, not a login loop.
[[ -f ~/.bashrc ]] && . ~/.bashrc
if [[ -z $WAYLAND_DISPLAY && $(tty) == /dev/tty1 ]]; then
  mkdir -p ~/.cache
  mazapan apply >~/.cache/mazapan-apply.log 2>&1
  start-hyprland >~/.cache/start-hyprland.log 2>&1
fi
