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

# myarch as built from the mounted repo, on everyone's PATH: the desktop
# runs it too (the command palette's actions), not just shells.
ln -sf "/home/$user/my-arch/bin/myarch" /usr/local/bin/myarch

# Quickshell's network module talks to NetworkManager; the cloud image
# ships systemd-networkd. The switch drops the network for a moment, which
# can take an SSH session (and this script) down with it, so it runs
# detached from us.
log "network: NetworkManager"
install -Dm644 "$here/wifi-lab/unmanaged.conf" /etc/NetworkManager/conf.d/myarch-wifi-lab.conf
if ! systemctl is-active -q NetworkManager; then
  systemctl disable systemd-networkd.service systemd-networkd.socket 2>/dev/null || true
  systemctl enable NetworkManager
  systemd-run --quiet --on-active=2 --unit=myarch-network-switch \
    sh -c 'systemctl stop systemd-networkd.socket systemd-networkd.service; systemctl start NetworkManager'
fi

# Two test Wi-Fi networks on simulated radios (mac80211_hwsim), so the
# network widget has something real to scan and join. VM only.
log "wifi test lab"
install -Dm644 -t /etc/myarch-wifi-lab "$here/wifi-lab/lab.conf" "$here/wifi-lab/open.conf" "$here/wifi-lab/dnsmasq.conf"
install -Dm644 -t /etc/systemd/system "$here/wifi-lab/myarch-wifi-lab.service"
systemctl daemon-reload
systemctl enable --now myarch-wifi-lab.service

log "bluetooth"
systemctl enable --now bluetooth.service

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
