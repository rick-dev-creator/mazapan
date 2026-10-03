# .NET, set up

ASP.NET Core and Aspire ready to use, without a step by hand. The .NET
profile in Apps installs the SDK and turns this on; it then:

- makes the HTTPS development certificate and trusts it: for .NET, for
  OpenSSL (`SSL_CERT_DIR`) and for Chromium's and Firefox's certificate
  stores, so `https://localhost` opens without a warning;
- installs Aspire's templates (`dotnet new aspire-starter`…);
- runs Aspire's containers on Podman (`containers`, podman: no root, no
  service) or Docker;
- puts dotnet's global tools (`~/.dotnet/tools`) on the PATH and turns its
  telemetry off (`telemetry_off`).

From the palette: ".NET: a new Aspire app" asks for a name and makes it in
`~/Projects`; "Set up .NET again" runs the setup in a terminal (its log is
`~/.cache/mazapan/dotnet-setup.log` otherwise). Its checks say whether the
SDK, Aspire's templates and the certificate are in order.

Rider and Visual Studio Code come with the profile from their makers:
Rider's Flatpak can't see the .NET SDK, and the C# Dev Kit runs only in
Microsoft's build of VS Code. Both are checked against their makers'
checksums and kept up to date by `mazapan update`.
