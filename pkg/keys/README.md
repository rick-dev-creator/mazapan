# Mazapán's signing keys

What `mazapan-keyring` puts in pacman's keyring, as archlinux-keyring
does: every package in Mazapán's repository is signed by one of these.

- `mazapan.gpg`: the public keys (`pkg/release keys KEYID pkg/keys`
  exports them).
- `mazapan-trusted`: the ones pacman trusts, `FINGERPRINT:4:` a line.
- `mazapan-revoked`: fingerprints no longer trusted, one a line.

A new key goes out in a keyring signed by a key installed systems
already trust (the old one), and the old one stays in mazapan-trusted
until every system has the new one.

Empty until the release key is made. A key is made once, kept offline,
and never committed: only its public half is here.

    gpg --quick-gen-key 'Mazapán release key' ed25519 sign never
