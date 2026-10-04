# Checkpoints

Before every change to the system's packages (an update, an install, a
removal) snapper takes a snapshot of the system (`hw-snapshots`). This
plugin makes them **checkpoints** anyone can use:

- **In the boot menu**, under *Checkpoints*, each one named for what it
  was before: "Oct 4, 14:02: before an update (12 packages)". The newest
  five (`entries`). Encrypted disks too: there `/boot` is the EFI
  partition, so each checkpoint's kernel and initramfs are kept there
  (`/boot/mazapan/k`, by content: the same ones are kept once).
- **Started from one, it says so**: a card as the desktop starts and a
  mark in the bar, for as long as it lasts. A checkpoint runs on an overlay
  in memory: what you change there is lost at the next start (your home
  folder is the same as always). Three answers:
  - **Keep this one**: it becomes your system. The one it replaces is
    kept `keep_days` days (7) as "the previous system", in the boot menu
    too, then deleted.
  - **Back to my system**: a restart.
  - **What broke?**: an agent gets what differs between the checkpoint and
    the main system (packages, Mazapán's last update, the errors of the
    start that failed), read only, and says what broke and how to fix it.
- **From History**, on the running system: "Restore this checkpoint"
  (from the next start), no boot menu needed.

From a terminal: `mazapan checkpoint` (where you are), `list`,
`diagnose [N]`, `sudo mazapan checkpoint keep`, `sudo mazapan checkpoint
restore N`. Agents (MCP) read `checkpoints` and `checkpoint_diagnose`;
keeping or restoring is yours.

Needs Mazapán's layout, the one its installer makes: btrfs with the
system in a subvolume of its own (`@`, snapper's snapshots nested in it),
GRUB, and mkinitcpio with systemd's initramfs (busybox's works through
grub-btrfs's hook, when it's installed). On another layout `mazapan
checkpoint` says so and leaves the menu alone.

How: after every pacman transaction (its hook, after mkinitcpio's) the
kernels `/boot` has are recorded and the checkpoints taken since get the
ones they started with; the menu is written to
`/boot/grub/mazapan-checkpoints.cfg`, which grub.cfg reads
(`/etc/grub.d/mazapan_checkpoints`), without grub-mkconfig. Keeping a
checkpoint renames `@` to `@-previous-DATE`, puts a writable copy of the
checkpoint in its place, moves the snapshots along and, with `/boot` the
EFI partition, puts its kernels back. A service at every start tells the
desktop which checkpoint this is and mounts the main system read only at
`/run/mazapan/main`.
