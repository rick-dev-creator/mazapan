# System snapshots

Where the system is on btrfs: a snapshot of `/` before and after every
package change (an update, an install, a removal), by snap-pac, with
snapper. The last `keep` changes are kept (10). They show in the
desktop's History with what changed; with `hw-checkpoints` they're in
the boot menu too, to start the system as it was.

Like any hardware plugin it's off until you turn it on:
`mazapan plugins enable hw-snapshots && mazapan apply --system` (it lists
what it will do as root first).

Best with `/home` on a subvolume of its own (archinstall's default): the
snapshots are of the system, not of your files (where `/home` is inside
`/`, undoing a package change's files would take yours back too).
`/.snapshots` is made listable, so the History panel can say what's there
without any rights over the snapshots; each file in them keeps its own
permissions.

Turned off, Mazapan stops managing it, but snap-pac keeps taking them
until it's removed (`sudo pacman -R snap-pac`, or `mazapan undo` right
after turning it on); the ones there stay (`sudo snapper -c root list`).
