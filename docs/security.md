# Security: what's protected, and what isn't

Audited 2026-10-04: three reviews of the code (what runs as root, what
listens or keeps secrets, where outside text could become code) and an
installed system looked at from inside. Nothing found lets someone without
the password into an encrypted Mazapan; what was found was fixed (see the
roadmap). What follows is the model, so nobody has to guess.

## Protected

- **The disk**: LUKS2, argon2id (about 2 s per try), everything inside it
  (swap, hibernation, snapshots). A recovery key, shown once (write it
  down). One password of at least 8 characters opens the disk, the account
  and the keyring; the account logs in by itself only right after the disk
  was opened with it.
- **Locked before it sleeps**, hibernation too; the lock screen stays if
  it crashes.
- **Nothing listening from outside** but what you turn on: the firewall
  denies everything in. SSH only when the install was given SSH keys (no
  passwords, no root). LocalSend from local networks only, and only while
  it runs. CUPS and the agents' telemetry receiver on 127.0.0.1 (the
  receiver also wants a token). Docker's published ports on 127.0.0.1
  (`local_only`), since Docker's own rules go around the firewall.
- **Updates signed**: Arch's packages and Mazapan's own repository, its key
  in mazapan-keyring. Apps from their makers (Rider, VS Code) checked
  against the maker's SHA-256.
- **Root only when asked**: every file written and command run as root is
  shown in full before sudo (`apply --system`, and `undo` of a root change).
  polkit asks for the password every time pacman runs from a panel.
  Checkpoints' root code never reads the person's home.
- **Agents**: through Mazapan's MCP tools, an agent can change numbers and
  switches only (no text settings: those can hold commands), nothing as
  root, and every change goes through the approval card with its diff.
  "Ask an agent" runs them read-only, with no keys, in an empty folder.
- **Outside text stays text**: notifications, window titles, clipboard,
  QR codes and file names are never run, nor shown as markup; answers and
  READMEs never load images by themselves; links open only if https.

## Not protected (know these)

- **Someone with the laptop in their hands for a while** (an "evil maid"):
  `/boot` is the unencrypted EFI partition and there's no Secure Boot yet,
  so a changed initramfs could capture the password the next time it's
  typed. As in stock Arch. Secure Boot with signed images
  (sbctl) is on the roadmap.
- **Programs you run are you**: anything running as your account (a
  project's npm or pip scripts, an agent's shell, a downloaded binary) can
  read your files and your keyring while you're logged in, as on any Linux
  desktop. Prefer agents' own sandboxes and permission prompts; Mazapan's
  approval card stops agents that use its tools, not ones with a shell of
  their own (they could change files directly).
- **API keys in the keyring** are readable by those same programs. An
  admin key (Anthropic's, OpenAI's) manages the whole organization: keep it
  only if you need the spend in the dashboard.
- **The docker group is root**: with Docker's `group` on, anything you run
  can get root through it. Podman (the default) has no such group.
- **A fingerprint is as good as the password** for polkit (installing,
  system settings) when hw-fingerprint is on; sudo in a terminal still
  asks the password.
- **Plugins from others** (`mazapan plugins add`) run their templates'
  commands as you, and their system files as root once you approve them:
  read what the capability review shows. A plugin in
  ~/.local/share/mazapan/plugins with a built-in's id takes its place.
- **An unattended install** (a stick labelled cidata) leaves the recovery
  key on the stick, in plain text: keep it safe or wipe it.
