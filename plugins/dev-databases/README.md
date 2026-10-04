# Databases for development

PostgreSQL, MySQL, Redis, SQL Server and MongoDB, one click each, from
"Databases for development" in the palette:

- **Start** makes it the first time (the image downloaded) and starts it;
  its data is kept in a volume of its own between starts.
- **Copy for .NET** / **Copy URL**: how to connect, password included, on
  the clipboard (marked secret: the clipboard history doesn't keep it).
- **Remove**, with its data, asks twice.

Each listens on this computer alone (127.0.0.1, its usual port: 5432,
3306, 6379, 1433, 27017). Its password is made the first time and kept in
`~/.local/state/mazapan/devdb`, readable by you alone; it reaches the
container through a file, never a command line. In containers on Podman
(Docker when it's the one installed), the same as `docker ps` shows.

From a terminal: `~/.local/share/mazapan/bin/devdb list | start NAME |
stop NAME | remove NAME | string NAME dotnet|url`.

The .NET and Development profiles in Apps turn it on.
