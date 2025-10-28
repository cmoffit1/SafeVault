SafeVault — Production readiness checklist

This file collects practical, minimal steps to prepare SafeVault for a first production deployment.

IMPORTANT: Always test any migration or deployment on a staging copy of your database and take a full backup before making schema changes.

1) Secrets & configuration
- Do NOT check secrets into source control.
- Use environment variables, Azure Key Vault, AWS Secrets Manager, or the 'dotnet user-secrets' store for local testing.
- Required production environment settings (examples):
  - ConnectionStrings__DefaultConnection   # connection string
  - TokenSettings__SigningKey              # JWT signing key (long random secret)
  - TokenSettings__Issuer
  - TokenSettings__Audience
  - TokenSettings__ExpirationMinutes
  These map to the configuration keys `ConnectionStrings:DefaultConnection` and `TokenSettings:...` respectively. ASP.NET Core's default configuration provider will read environment variables where `__` is used as the section separator.
- Avoid storing secrets in `appsettings.Production.json`. Use a secret store or environment variables provided by your hosting platform.
- Ensure `SeedAdmin:Enabled` is false in production. If you need to seed an admin account for the first deployment, do it explicitly and then disable the flag.

2) Database migration
- A migration script has been added at `DB/migrations/20251023_normalize_roles.sql`.
- Run the script against a staging DB first. Verify the `Roles` and `UserRoles` tables populated correctly.
- Backup production DB before running. The script is idempotent but backups are mandatory.

3) Security and network
- Restrict CORS to the specific frontend origin(s) in production configuration.
- Ensure HTTPS is enforced via reverse proxy (Kestrel + TLS) or load balancer, and enable HSTS.
- Confirm Content-Security-Policy is suitable for your app (images/CDN etc.).

4) Logging and monitoring
- Configure structured logging (Serilog recommended) and send logs to a durable sink.
- Add health and readiness endpoints for orchestrators.
- Configure monitoring/alerts (CPU, memory, failed requests, error rate).

5) CI/CD and deployment
- Create a CI pipeline to run `dotnet format`, `dotnet build`, `dotnet test` on pull requests.
- Add a deployment pipeline that requires manual approval for production.
- Consider containerizing the API (Dockerfile) for reproducible deployments.

6) Rollback and post-deploy checks
- Have a tested rollback plan (DB restore, previous artifact redeploy).
- After deploy: verify authentication flow, admin role functionality, and audit logs.

7) Additional notes
- The system uses Argon2id for password hashing. Ensure your production environment has adequate CPU/memory for hashing and increase time/memory parameters conservatively.
- The migration only backfills role data from the old `Users.Roles` column; it does not remove the column. A follow-up migration can drop it when safe.

If you want, I can:
- Add a PowerShell migration runner (`scripts/run-migration.ps1`) that prompts for connection details and runs the SQL safely.
- Add a Dockerfile and a minimal GitHub Actions workflow for CI.
- Add Serilog configuration and a health endpoint.

Tell me which of the above I should implement next and I’ll do it.
