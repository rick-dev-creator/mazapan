# Bases de datos para desarrollo

PostgreSQL, MySQL, Redis, SQL Server y MongoDB, a un clic cada una, desde
«Bases de datos para desarrollo» en la paleta:

- **Arrancar** la crea la primera vez (descargando la imagen) y la pone en
  marcha; sus datos se guardan en un volumen propio entre arranques.
- **Copiar para .NET** / **Copiar URL**: cómo conectarse, contraseña
  incluida, en el portapapeles (marcado como secreto: el historial del
  portapapeles no lo guarda).
- **Borrar**, con sus datos, pregunta dos veces.

Cada una escucha solo en este equipo (127.0.0.1, su puerto habitual: 5432,
3306, 6379, 1433, 27017). Su contraseña se crea la primera vez y se guarda
en `~/.local/state/mazapan/devdb`, legible solo por ti; llega al contenedor
por un archivo, nunca por la línea de comandos. En contenedores de Podman
(Docker si es el que está instalado), los mismos que muestra `docker ps`.

Desde una terminal: `~/.local/share/mazapan/bin/devdb list | start NOMBRE |
stop NOMBRE | remove NOMBRE | string NOMBRE dotnet|url`.

Los perfiles .NET y Desarrollo de Apps lo activan.
