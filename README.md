# SafeVault

This repository contains a Blazor WebAssembly client and an ASP.NET Core minimal API backend focused on secure input handling and authentication.

## Projects
- Api/ - ASP.NET Core minimal API (targets .NET 10)
- Client/ - Blazor WebAssembly client (targets .NET 8)
- Api.Tests/ - NUnit test project (unit + integration tests)

## Prerequisites
- .NET SDK 8.x and 10.x installed (build uses the installed SDK(s)).
  - The API project targets .NET 10. If you don't have .NET 10 SDK installed, you can still build and run tests for the API using a compatible SDK; however, having .NET 10 preview/GA is recommended for parity.
- PowerShell (Windows) or a POSIX shell on other platforms.

## Build
From the repository root (where `SafeVault.sln` is located):

```powershell
# Restore and build all projects
dotnet restore
dotnet build
```

If you only want to build one project:

```powershell
# Build the API
dotnet build Api/Api.csproj

# Build the Client
dotnet build Client/Client.csproj
```

## Run
There are VS Code tasks in the workspace that run the API and Client. You can run them via the command line or from within an editor that supports launching tasks.

From the terminal you can run each project directly:

```powershell
# Run the API (background)
dotnet run --project Api/Api.csproj

# In a separate terminal, run the Client (Blazor WebAssembly)
dotnet run --project Client/Client.csproj
```

Open the client in the browser when the Blazor dev server reports the URL. The client calls the API endpoints on the configured base address.

## Tests
Run unit and integration tests (NUnit):

```powershell
dotnet test Api.Tests/Api.Tests.csproj
```

The test project includes:
- Unit tests for input validation (SQLi/XSS patterns)
- Unit tests for LoginUserManager behavior
- Integration tests using WebApplicationFactory against the API endpoints (/register and /authenticate)

## Tasks (VS Code)
There are tasks configured to run the API and the Client (see `.vscode/tasks.json` if you want to customize them). You can run them via the editor Run/Tasks UI.

## Notes on Target Frameworks and Packages
- The API is currently set to target .NET 10 (net10.0).
- The Client targets .NET 8 (net8.0) and references stable Blazor packages (Microsoft.AspNetCore.Components.WebAssembly and Microsoft.AspNetCore.Components.Web v8.0.0). This was done to avoid depending on preview/RC packages in the workspace; you can update to net10.0 and pin preview packages if you prefer to run everything on .NET 10 preview.

## Security and Testing
- Input is sanitized using project helpers in `Api/InputValidation.cs` and `Api/Utilities/ValidationHelpers.cs`.
- Passwords are hashed using PBKDF2 (`Api/Utilities/PasswordHasher.cs`).
- The `IUserRepository` abstraction allows swapping in a database-backed repository (`Api/Repositories/UserRepository.cs`) or the in-memory test repository (`Api/Repositories/InMemoryUserRepository.cs`).

## Next Steps and Suggestions
- If you want the Client to target .NET 10 GA (when available), update `Client/Client.csproj` and the package versions accordingly.
- To use a real database during integration tests, configure a test database and update the `Api` configuration (connection string) before running the DB-backed tests.

If you want, I can:
- Pin client to net10.0 and add explicit preview package references (if you're targeting .NET 10 preview), or
- Keep the current net8.0 client and add CI that builds and runs the tests on push/PR.

## Secrets & local development

To enable JWT token issuance locally, provide a secure SigningKey for `TokenSettings:SigningKey`. Do not check this into source control. Options:

- Use `dotnet user-secrets` (recommended for local dev):

```powershell
cd Api
dotnet user-secrets init
dotnet user-secrets set "TokenSettings:SigningKey" "<LONG_RANDOM_BASE64_OR_HEX>"
```

- Use environment variables (example for PowerShell):

```powershell
$env:ASPNETCORE_ConnectionStrings__DefaultConnection = "Server=...;Database=...;User Id=...;Password=...;"
$env:TokenSettings__SigningKey = "<LONG_RANDOM_SECRET>"
```

### Generate a secure signing key (PowerShell helper)

You can generate a 256-bit (or larger) random key with the helper script in `scripts/generate-signing-key.ps1` and copy it into user-secrets or an env var.

## How to run locally (recommended)

This repository contains two runtimes: the API (ASP.NET Core) and a Blazor WebAssembly client. Below are two safe ways to run the app locally.

1) Development (fast iteration for API + Blazor dev host)

```powershell
# From the repo root
dotnet restore
dotnet build

# Run the API (in a dedicated terminal)
dotnet run --project Api/Api.csproj

# In a second terminal, run the Blazor dev host for the client
dotnet run --project Client/Client.csproj
```

Notes:
- If you see "Cannot find runtime config" when starting the client, run `dotnet build Client/Client.csproj` first and retry `dotnet run`.

2) Stable local hosting (recommended for manual QA)

Use the published client files and the included static host in `Tools/ClientHost`. This avoids SDK host quirks and serves the same files used in production.

```powershell
# Publish the client to Client/publish (creates Client/publish/wwwroot)
dotnet publish Client/Client.csproj -c Debug -o Client\publish

# Start the static host that serves the published client (serves on https://localhost:5161)
dotnet run --project Tools/ClientHost/ClientHost.csproj

# In a separate terminal, start the API if not already running
dotnet run --project Api/Api.csproj
```

Open the SPA at: https://localhost:5161 and the API at https://localhost:7067 (or http://localhost:5046). If you change ports, check the browser console / network tab for failed API calls.

Tests

```powershell
# Run unit + integration tests
dotnet test Api.Tests/Api.Tests.csproj
```

Troubleshooting
- TLS / browser trust: your browser may warn about the dev TLS certificate for localhost. Accept the certificate for local testing or use the API HTTP endpoint on port 5046.
- Runtime config error when running the client: run `dotnet build Client/Client.csproj` before `dotnet run` or prefer the publish + ClientHost flow.
- If the SPA fails to load `_framework` assets (404s), confirm you served `Client/publish/wwwroot` and that the host is serving that folder.

If you'd like, I can add a PowerShell helper script `start-local.ps1` that bundles the publish + host + api start steps.


