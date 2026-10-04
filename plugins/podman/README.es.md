# Podman

Contenedores listos para usar, sin root ni un servicio corriendo siempre:

- `docker run …`, `docker compose up` funcionan tal cual: `docker` es
  Podman (`~/.local/bin/docker`; si Docker está instalado, gana el suyo);
- el socket de la API de Podman arranca al iniciar sesión, y `DOCKER_HOST`
  apunta a él, así que lo que busca Docker lo encuentra: Testcontainers
  (con su limpiador, Ryuk, apagado: Podman sin root no le puede dar un
  socket), los devcontainers de VS Code, Compose, .NET Aspire;
- Podman Desktop (en Apps) sin su telemetría, y encuentra este Podman
  solo;
- sin un grupo con poder de root, sin daemon como root, sin puertos
  publicados saltándose el firewall.

Podman en Apps lo activa (los perfiles Desarrollo y .NET lo incluyen), o
`mazapan plugins enable podman && mazapan apply --system`. Con el plugin
docker activo también, se usa el socket del propio Docker.
