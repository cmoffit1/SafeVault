SafeVault — ARCHITECTURE

This document describes the high-level architecture, component responsibilities, data flows, configuration, and extension points for the SafeVault solution.

Repository layout (important folders)

- Api/
  - Controllers & Endpoints: endpoints are defined in `Program.cs` using minimal APIs (e.g., `/register`, `/authenticate`, `/validate`, `/weatherforecast`).
  - Models/: domain models (e.g., `LoginUser.cs`). Models hold data shapes and light validation.
  - Services/: application services that orchestrate logic and coordinate domain operations. Key service: `LoginUserManager` (handles registration and authentication logic, enforces password policy, coordinates hashing and repository storage).
  - Repositories/: persistence abstractions and implementations.
    - `IUserRepository` defines storage operations (add user, get hashed password).
    - `InMemoryUserRepository` is an in-memory store used for development and tests.
    - `UserRepository` is a SQL-backed implementation that uses parameterized queries (Microsoft.Data.SqlClient).
  - Utilities/: reusable helpers.
    - `PasswordHasher` (PBKDF2 hashing/verification)
    - `PasswordPolicy` (configurable min/max lengths)
    - `ValidationHelpers` (input normalization, username sanitizer, HTML sanitizer/XSS detection)
  - Dtos/: data transfer objects for endpoints (register/authenticate payloads).

- Client/
  - Blazor application (separate project). UI components live under `Pages/` and `Layout/`.

- Api.Tests/
  - Unit and integration-like tests. Tests exercise services, utilities, DI registrations, and API endpoints.

Key design principles

- Separation of concerns: services orchestrate, repositories persist, utilities provide shared functionality.
- Testability: interfaces for repositories and hasher make mocking and in-memory substitution straightforward.
- Security by design: parameterized SQL queries, PBKDF2 password hashing, input sanitization/normalization, basic CSP and security headers, generic exception handling.
- Config-driven behavior: password policy and allowed CORS origins can be supplied via configuration (see Configuration Keys below).

Data flow: user registration (simplified)

1. Client -> POST /register (RegisterRequest DTO)
2. `Program.cs` maps the endpoint and constructs a `LoginUser` model from DTO.
3. `LoginUserManager.RegisterAsync(user)` executes:
   - Sanitizes and normalizes the username (`ValidationHelpers.SanitizeUsername`) and trims the password.
   - Runs input validation (`LoginUser.IsValid`).
   - Enforces username length and `PasswordPolicy` (config-driven).
   - Hashes the password with `PasswordHasher.Hash` (PBKDF2 with per-user salt).
   - Calls `IUserRepository.AddUserAsync(normalizedUsername, hashed)` to persist. The default DI registration uses `InMemoryUserRepository` when `DefaultConnection` is not configured.
   - Returns success or failure for the endpoint.

Data flow: authentication

1. Client -> POST /authenticate (AuthenticateRequest DTO)
2. Endpoint calls `LoginUserManager.AuthenticateAsync(username, password)`.
3. Manager normalizes username with `SanitizeUsername`, fetches stored hash via `IUserRepository.GetHashedPasswordAsync`, and verifies the provided password with `PasswordHasher.Verify`.
4. Manager logs auth attempts (without sensitive data) and returns boolean result.

Configuration keys

- ConnectionStrings:DefaultConnection
  - When set, the API registers `UserRepository` (SQL-backed) as the `IUserRepository` implementation. When not set, `InMemoryUserRepository` is used.

- AllowedOrigins
  - Optional array of allowed CORS origins (if omitted, the app is permissive to avoid breaking local development).

- PasswordPolicy
  - Optional object with `MinLength` and `MaxLength` properties. If provided it will be bound to `PasswordPolicy` and used by `LoginUserManager`.

Logging and observability

- Structured logging is used via `ILogger<T>` in `LoginUserManager`, `InMemoryUserRepository`, and `UserRepository`.
- Sensitive values (passwords, password hashes) are never logged.
- For production, configure logging sinks (console, file, central aggregator) via `appsettings.json`.

Security considerations and OWASP alignment

- Use parameterized queries in `UserRepository` to prevent SQL injection.
- Do not mutate secrets during sanitization (passwords are trimmed only).
- Use allow-list username sanitizer for identifiers and general text normalization for free text.
- Add production exception handler to avoid leaking stack traces.
- Add security headers (Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy).
- Next improvements: rate-limiting, account lockouts, audit trail, fortified CSP tailored to Blazor client.

Extensibility points

- Add another IUserRepository implementation for different data stores (e.g., EF Core, Dapper, Redis). Implement `IUserRepository` and register it in DI.
- Replace `PasswordHasher` with an alternative KDF (e.g., Argon2) by implementing `IPasswordHasher` and registering it.
- Add additional policies in `PasswordPolicy` (complexity rules) and update tests.

Run & test

- Build: `dotnet build`
- Tests: `dotnet test Api.Tests/Api.Tests.csproj`
- Run API: `dotnet run --project Api/Api.csproj` (use `DefaultConnection` in `appsettings.json` to switch to SQL-backed repository)

Notes for maintainers

- Keep `ValidationHelpers` behavior stable: username sanitizer is intentionally strict and used for storage keys; general `Sanitize` is for normalizing free text.
- Avoid adding input sanitization that mutates secrets. Always use parameterized queries and allow-listing for identifiers.

Naming conventions (project standards)

- Types and classes: PascalCase (e.g., `LoginUser`, `PasswordPolicy`, `LoginUserManager`).
- Interfaces: prefix with `I` and use PascalCase (e.g., `IUserRepository`, `IPasswordHasher`).
- Async methods: use the `Async` suffix for Task-returning methods (e.g., `RegisterAsync`, `GetHashedPasswordAsync`).
- Utilities: name helpers by intent using nouns or noun phrases (e.g., `PasswordHasher`, `HtmlSanitizer`, `InputNormalizer`).
- Repositories: concrete repository types should describe the backing store (e.g., `InMemoryUserRepository`, `UserRepository`).
- DTOs: end with `Request`/`Response` or `Dto` (e.g., `RegisterRequest`, `AuthenticateRequest`).
- Logging: use `ILogger<T>` where `T` is the owning class and avoid logging secrets (passwords, hashes, tokens).
- Configuration keys: keep keys PascalCase and mirror object names where sensible (e.g., `PasswordPolicy:MinLength`).

Contact and further steps

If you'd like, I can:
- Add `appsettings.example.json` showing the `AllowedOrigins`, `PasswordPolicy`, and a placeholder `DefaultConnection`.
- Split `ValidationHelpers` into smaller focused classes.
- Add integration tests against a disposable/test SQL instance to exercise `UserRepository`.

*** End of file
