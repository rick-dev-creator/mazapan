# System snapshots in the boot menu

With `hw-snapshots` on and GRUB as the boot loader: the snapshots in the
boot menu ("Arch Linux snapshots"), kept up to date by grub-btrfsd. An
update that leaves the system unable to start: pick the snapshot from
before it, and the whole desktop starts as it was. It runs on an overlay
in memory: what you do there goes at the next restart; it's for getting
going again (and undoing what broke), not a place to stay. Snapshots
taken before this was on start only partly: the initramfs they carry
lacks the overlay.

Offered only where `/boot` is on the root filesystem, so each snapshot
has its own kernel: with `/boot` a partition of its own (the EFI one,
archinstall's default), a snapshot would start with today's kernel and
its own, older modules. Needs mkinitcpio (not dracut); a drop-in of
yours in `/etc/mkinitcpio.conf.d` that sets `HOOKS=(…)` after this one
takes the overlay out (`myarch doctor` can't see inside the initramfs).

Off until you turn it on: `myarch plugins enable hw-snapshots-grub &&
myarch apply --system`. Turned off, grub-btrfsd stops and the entries
already there stay bootable; remove grub-btrfs to take them out of the
menu. Limine: not yet (limine-snapper-sync isn't in Arch's repositories).
