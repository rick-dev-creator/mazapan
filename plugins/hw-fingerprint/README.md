# Fingerprint reader

A finger, beside the password:

- **unlocks the screen** (hyprlock asks fprintd itself);
- **allows system changes**: Mazapan's password prompts (polkit) take a
  finger first, the password still works;
- with the lid closed (the reader under it) the password is asked straight
  away.

"Fingerprint: add a finger" in the palette enrolls one (touch the reader a
few times); "Fingerprint: remove your fingers" forgets them. `sudo` in a
terminal still asks the password: its PAM file is the sudo package's own,
which Mazapan never edits.

What it does as root: installs `fprintd`, and writes `/etc/pam.d/polkit-1`,
polkit's own service (kept in `/usr/lib/pam.d`) with the finger first;
turned off, the file goes and polkit's own counts again.

Offered on machines with a reader fprintd supports (Goodix, Synaptics,
Elan, Validity, Egis, FocalTech, FPC…); the installer turns it on there.
