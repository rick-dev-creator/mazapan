# .NET, configurado

ASP.NET Core y Aspire listos para usar, sin un paso a mano. El perfil .NET
de Apps instala el SDK y enciende esto; entonces:

- crea el certificado HTTPS de desarrollo y lo hace de confianza: para
  .NET, para OpenSSL (`SSL_CERT_DIR`) y para los almacenes de certificados
  de Chromium y Firefox, así `https://localhost` abre sin aviso;
- instala las plantillas de Aspire (`dotnet new aspire-starter`…);
- ejecuta los contenedores de Aspire en Podman (`containers`, podman: sin
  root ni servicio) o en Docker;
- pone las herramientas globales de dotnet (`~/.dotnet/tools`) en el PATH y
  apaga su telemetría (`telemetry_off`).

Desde la paleta: «.NET: una app Aspire nueva» pide un nombre y la crea en
`~/Projects`; «Configurar .NET otra vez» ejecuta la configuración en una
terminal (si no, su registro está en `~/.cache/mazapan/dotnet-setup.log`).
Sus comprobaciones dicen si el SDK, las plantillas de Aspire y el
certificado están en orden.

Rider y Visual Studio Code vienen con el perfil de sus fabricantes: el
Flatpak de Rider no ve el SDK de .NET, y el C# Dev Kit solo funciona en la
versión de Microsoft de VS Code. Ambos se verifican con las sumas de sus
fabricantes y `mazapan update` los mantiene al día.
