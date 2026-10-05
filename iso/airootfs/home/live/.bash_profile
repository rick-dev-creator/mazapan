# The live session: mazapan writes the desktop, then Hyprland starts. Not
# exec'd: if Hyprland dies there's a shell on tty1, not a login loop.
[[ -f ~/.bashrc ]] && . ~/.bashrc
if [[ -z $WAYLAND_DISPLAY && $(tty) == /dev/tty1 ]]; then
  mkdir -p ~/.cache
  mazapan apply >~/.cache/mazapan-apply.log 2>&1
  started=$SECONDS
  start-hyprland >~/.cache/start-hyprland.log 2>&1
  # Back here within moments: the desktop couldn't start (graphics the
  # ISO's drivers don't handle yet). The install doesn't need it.
  if (( SECONDS - started < 60 )); then
    printf '\n\e[1mThe desktop could not start on this computer'"'"'s graphics.\e[0m\n'
    printf 'What it said: ~/.cache/start-hyprland.log\n'
    printf 'Mazapan can still be installed from here:  \e[1mmazapan-install-text\e[0m\n'
    printf 'Or try the desktop again:                  start-hyprland\n\n'
  fi
fi
