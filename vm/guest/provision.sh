#!/usr/bin/env bash
# Runs as root inside the VM. Idempotent: safe to re-run after editing
# packages.txt (vm/vm provision).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
user=arch

log() { printf '\e[1;36m[provision]\e[0m %s\n' "$*"; }

# The cloud image seeds its keyring on first boot; make sure it's done.
if ! pacman-key --list-keys >/dev/null 2>&1; then
  log "initializing pacman keyring"
  pacman-key --init
  pacman-key --populate archlinux
fi

log "updating system and installing packages"
mapfile -t pkgs < <(sed -e 's/#.*//' -e '/^\s*$/d' "$here/packages.txt")
pacman -Sy --noconfirm --needed archlinux-keyring
pacman -Su --noconfirm
pacman -S --noconfirm --needed "${pkgs[@]}"

log "locale"
if ! locale -a 2>/dev/null | grep -qi '^en_US.utf8$'; then
  sed -i 's/^#\(en_US.UTF-8 UTF-8\)/\1/' /etc/locale.gen
  locale-gen
fi
echo 'LANG=en_US.UTF-8' > /etc/locale.conf

# cloud-init's mounts module can create /home/$user (for the 9p mount point)
# before the user exists, leaving it root-owned and without skel files.
log "fixing home ownership"
chown "$user:$user" "/home/$user"
for f in /etc/skel/.[!.]*; do
  [[ -e /home/$user/${f##*/} ]] || install -o "$user" -g "$user" -m 644 "$f" "/home/$user/"
done

log "autologin on tty1"
install -d /etc/systemd/system/getty@tty1.service.d
cat > /etc/systemd/system/getty@tty1.service.d/autologin.conf <<EOF
[Service]
ExecStart=
ExecStart=-/usr/bin/agetty --autologin $user --noclear %I \$TERM
EOF
systemctl daemon-reload
# On first boot tty1 is already sitting at a login prompt; pick up the
# autologin now. Never do this under a running session.
pgrep -u "$user" -x Hyprland >/dev/null || systemctl restart getty@tty1

log "start Hyprland on tty1 login"
profile="/home/$user/.bash_profile"
marker="# my-arch: start Hyprland"
if ! grep -qF "$marker" "$profile" 2>/dev/null; then
  cat >> "$profile" <<EOF

$marker
# Not exec'd: if Hyprland dies you land in a shell on tty1 instead of a
# crash/autologin loop.
if [[ -z \$WAYLAND_DISPLAY && \$(tty) == /dev/tty1 ]]; then
  mkdir -p ~/.cache && start-hyprland >~/.cache/start-hyprland.log 2>&1
fi
EOF
  chown "$user:$user" "$profile"
fi

log "done"
