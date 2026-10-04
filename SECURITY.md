# Security policy

## Reporting a vulnerability

Please **don't open a public issue** for a security problem. Report it
privately instead, through GitHub:
[**Report a vulnerability**](https://github.com/rick-dev-creator/mazapan/security/advisories/new)
(the repository's Security tab).

Say what's affected, how to see it happen, and what someone could do with
it. You'll hear back within a week; once it's fixed, the advisory is made
public with credit to you, unless you'd rather not be named.

## What's in scope

- The `mazapan` core: anything that runs as root (`apply --system`,
  checkpoints, the installer), the MCP server and the approval of agents'
  changes, the handling of passwords, keys and the keyring.
- The built-in plugins (`plugins/`) and the ISO (`iso/`).
- A way for text from outside (a notification, a window title, a file
  name, an agent's answer, a plugin's README) to become a command or markup.

Plugins from others (`mazapan plugins add`) are their authors'; a way for
one to do more than its capability review showed is in scope.

## Supported versions

Mazapan is young: fixes go to the latest release and to `main`.

| Version | Supported |
|---|---|
| latest release, `main` | yes |
| anything older | no: update |

## What's protected, and what isn't

The threat model (the disk, the network, root, agents, outside text) and
its known limits are written down in [docs/security.md](docs/security.md).
