# Changelog

What changed in each release, newest first. The updates panel shows what's
new since the version a system has. Changes go under "Unreleased" until a
release (a tag `vX.Y.Z`) names them.

## Unreleased

- Mazapán updates itself: its own signed repository, in two channels
  (stable, and edge with every release first): `mazapan channel` says which
  one and switches, `mazapan version` says the version.
- Updates in a few steps, each with its ✓, from the terminal or the panel:
  room and power checked and the machine kept awake, the keyrings first,
  a failed initramfs rolled back, what needs a restart offered, what's new
  in Mazapán shown. A new Mazapán finishes the update it came in.
- The disk encrypted by default, with a recovery key (shown as text and a
  code to photograph) and one password: typed as the computer starts, it
  logs in and opens the keyring.
- Locked before it sleeps: the lid never opens on an unlocked desktop.
- The firewall on: nothing comes in that wasn't asked for.
