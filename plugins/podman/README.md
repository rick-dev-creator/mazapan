# Podman

Containers ready to use, without root or a service running all the time:

- `docker run …`, `docker compose up` work as typed: `docker` is Podman
  (`~/.local/bin/docker`; Docker's own wins when it's installed);
- Podman's API socket starts at login, and `DOCKER_HOST` points to it, so
  what looks for Docker finds it: Testcontainers (its reaper, Ryuk, off:
  rootless Podman can't give it a socket), VS Code's devcontainers,
  Compose, .NET Aspire;
- Podman Desktop (in Apps) without its telemetry, and it finds this
  Podman on its own;
- no group that is as powerful as root, no daemon as root, no port
  published past the firewall.

Podman in Apps turns it on (the Development and .NET profiles include it),
or `mazapan plugins enable podman && mazapan apply --system`. With the
docker plugin on as well, Docker's own socket is the one used.
