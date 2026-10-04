# Docker

Docker de verdad, listo, para lo que necesita Docker y no Podman:

- `docker.socket` en marcha en cada arranque (el daemon arranca con el
  primer comando `docker`);
- tú en el grupo `docker` (`group`, encendido), así que `docker` no
  necesita sudo, desde el próximo arranque (también al cambiarlo: los
  contenedores en marcha no se paran por eso). Quien está en ese grupo
  puede hacer todo lo que root: un contenedor puede montar todo el disco.
  Apagado: `sudo docker`.
- Compose y Buildx.

Docker publica los puertos de los contenedores saltándose el firewall (sus
reglas van primero): un puerto publicado como `-p 8080:80` queda abierto a
la red; para que sea solo de este equipo, `-p 127.0.0.1:8080:80`.

Docker en Apps lo activa. Al desactivarlo: Docker se para, deja de
arrancar y sales de su grupo. El plugin Podman es la opción ligera (sin
root, sin servicio); con los dos activos, `docker` es el de Docker.
