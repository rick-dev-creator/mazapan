# Mazapan's signing keys

What `mazapan-keyring` puts in pacman's keyring, as archlinux-keyring
does: every package in Mazapan's repository is signed by one of these.

- `mazapan.gpg`: the public keys (`pkg/release keys KEYID pkg/keys`
  exports them).
- `mazapan-trusted`: the ones pacman trusts, `FINGERPRINT:4:` a line.
- `mazapan-revoked`: fingerprints no longer trusted, one a line.

A new key goes out in a keyring signed by a key installed systems
already trust (the old one), and the old one stays in mazapan-trusted
until every system has the new one.

The release key: `Mazapan release key <release@mazapan.dev>`,
fingerprint `7C05 EB75 B1C5 4AD8 F9E1  61C7 1FCE 814E 8077 F475`
(ed25519, signing only, made 2026-10-05). Only its public half is here;
the secret half and its revocation certificate are kept offline, never
committed.
