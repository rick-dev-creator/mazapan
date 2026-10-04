# Installing on a real machine (the first time)

Mazapán has been tried in virtual machines; this is the checklist for
the first real one. Written for a laptop with hybrid graphics (an AMD or
Intel GPU and an NVIDIA one, such as the MSI Vector A16 HX), and true
for most machines.

## Before

- **Back up** whatever is on the disk: the install erases it whole.
- **The firmware (BIOS/UEFI) settings**, usually with Del or F2 as it
  starts:
  - **Secure Boot: off.** The USB stick and the installed boot loader
    aren't signed.
  - **Graphics: Hybrid** (MSHybrid, Optimus), not "Discrete only": the
    screen then runs on the integrated GPU, which works from the stick.
    It can be switched later (then `mazapan hardware` and
    `mazapan apply --system`).
  - UEFI boot (not "Legacy"/CSM).
- **The stick:** write the ISO whole, with `dd` or Fedora Media Writer
  (Ventoy may work, but not for the first try).
- **Offline the first time** (no cable, Wi-Fi off): the install then uses
  only what's on the stick, the same packages that were tested. It
  updates itself after.

## Starting the stick

The boot menu has **mazapan** and **mazapan, safe graphics (NVIDIA
off)**. Take the first; if the screen stays black or flickers, restart
and take the second.

If the desktop can't start at all, the console says so and offers
**`mazapan-install-text`**: the same install, asked in text. If the
screen stays black with no console: Ctrl+Alt+F2, log in as `root` (no
password on the stick) and run `mazapan-install-text`.

## Installing

- The disk is encrypted, with one password for the disk and the account.
  **Write down the recovery key** it shows at the end (also as a QR code
  to photograph): it opens the disk if the password is forgotten.
- Keep the keyboard layout you'll type the password in: at every start
  it's asked before anything else.

## The first start

- The disk's password, typed on the boot screen (if it stays black, type
  it anyway and press Enter: it's listening).
- Then, in a terminal:
  - `mazapan doctor`: every check should pass.
  - `systemctl --failed`: nothing.
  - NVIDIA: `nvidia-smi` shows the card; `prime-run glxinfo | grep
    renderer` says NVIDIA; `dkms status` says installed.
  - `mazapan checkpoint`: "1 checkpoint in the boot menu".
- **Sleep:** close the lid, open it: the screen comes back and the
  session is there. Then the same after a few minutes (it may hibernate).
- **Screen scale:** a 16" 2560×1600 screen looks best at 1.6 (Monitors,
  SUPER + SHIFT + M).
- An external monitor on the laptop's HDMI port is on the NVIDIA card:
  it works, with a little more delay than the built-in one.

## If something goes wrong

- **It doesn't start after an update:** in the boot menu, *Checkpoints*,
  take the one from before it. Once on it, the card says what to do:
  keep it, go back, or ask an agent what broke.
- **What happened, for help:** `mazapan report` writes what's needed
  (logs, hardware, versions) to one file to share.
