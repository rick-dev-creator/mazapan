# Docker

Docker itself, ready, for what needs Docker and not Podman:

- `docker.socket` started at every start (the daemon starts with the first
  `docker` command);
- you in the `docker` group (`group`, on), so `docker` needs no sudo, from
  the next start (a change of it too: running containers aren't stopped
  for it). Anyone in that group can do anything root can: a
  container can mount the whole disk. Off: `sudo docker`.
- Compose and Buildx.

Docker publishes containers' ports past the firewall (its own rules come
first): a port published as `-p 8080:80` is open to the network; bind it to
this computer alone with `-p 127.0.0.1:8080:80`.

Docker in Apps turns it on. Turned off: Docker stopped, not started any
more, you out of its group. The Podman plugin is the lighter choice (no
root, no service); with both on, `docker` is Docker's own.

Ports containers publish listen on this computer only (`local_only`, on): Docker writes its own firewall rules, which go around ufw, so a project database published with `-p 5432:5432` would otherwise be open to any network you join. Turn it off, or write `-p 0.0.0.0:5432:5432`, to share one on purpose.
