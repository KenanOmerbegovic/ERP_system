# ERP

Modular monolith ERP for Bosnia and Herzegovina: Erp.Api (.NET 10, PostgreSQL) and a Quasar client
in [client/](client/README.md) for web and desktop.

## Local setup

Prerequisites: .NET 10 SDK, Docker, Node.js (for the client).

1. Start the database: `docker compose up -d` (PostgreSQL on localhost:5433).
2. One-time: give the API a JWT signing key in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
   (stored in your user profile, never in the repository):

   PowerShell:

   ```powershell
   $bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
   dotnet user-secrets set "Identity:Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project src/Erp.Api
   ```

   Bash:

   ```bash
   dotnet user-secrets set "Identity:Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/Erp.Api
   ```

   The API refuses to start if the key is missing, shorter than 32 bytes, or a known placeholder.
   Changing the key signs everyone out.
3. Run the API: `dotnet run --project src/Erp.Api` (http://localhost:5094; migrates the database in Development).
4. Mock data: `dotnet run --project tools/Erp.Seeder -- --size small --password <password>`.
5. Client: see [client/README.md](client/README.md).

Outside Development there are no user secrets: set `Identity__Jwt__SigningKey` from the host's secret store.

## Tests

`dotnet test` (needs Docker running; integration and seeder tests use Testcontainers).
