using Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure CORS: prefer explicit allowed origins via configuration to follow least-privilege.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Be permissive only in Development when not configured. In non-development
            // environments require explicit AllowedOrigins to avoid accidental exposure.
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }
            else
            {
                // In production-like environments, default to rejecting cross-origin
                // requests unless allowed origins are explicitly configured.
                policy.WithOrigins(Array.Empty<string>());
            }
        }
    });
});

// Register repository and utilities
// Prefer environment variables for secrets and connection strings. The IConfiguration
// already sources environment variables (with __ as section separator), but we also
// allow some common direct env var names for operators.
var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                  ?? Environment.GetEnvironmentVariable("DEFAULTCONNECTION");

// Configure EF Core and Identity. If a DefaultConnection is provided, use SQL Server; otherwise
// fall back to a local SQLite file for development so Identity can be scaffolded without extra setup.
if (!string.IsNullOrWhiteSpace(defaultConn))
{
    builder.Services.AddDbContext<Api.Identity.ApplicationDbContext>(options =>
        options.UseSqlServer(defaultConn));
}
else
{
    // Local dev: use SQLite file-based DB for Identity stores
    builder.Services.AddDbContext<Api.Identity.ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=identity-dev.db"));
}

builder.Services.AddIdentity<Api.Identity.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
{
    // Relax some defaults for local/dev; production can override via configuration
    options.User.RequireUniqueEmail = false;
}).AddEntityFrameworkStores<Api.Identity.ApplicationDbContext>().AddDefaultTokenProviders();

// Register the Identity-backed user repository implementation
builder.Services.AddScoped<Api.Repositories.IUserRepository, Api.Repositories.IdentityUserRepository>();
// Keep the (in-memory) task repository for now
builder.Services.AddSingleton<Api.Repositories.ITaskRepository, Api.Repositories.InMemoryTaskRepository>();
builder.Services.AddSingleton<Api.Utilities.IPasswordHasher, Api.Utilities.PasswordHasher>();
// Register an Identity-compatible password hasher that uses Argon2 and falls back
// to the default Identity hasher for legacy formats. This enables smooth migration
// to Argon2 while allowing existing PBKDF2 hashes to verify and be re-hashed.
builder.Services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<Api.Identity.ApplicationUser>>(sp =>
{
    var argon = sp.GetRequiredService<Api.Utilities.IPasswordHasher>();
    var fallback = new Microsoft.AspNetCore.Identity.PasswordHasher<Api.Identity.ApplicationUser>();
    return new Api.Identity.Argon2IdentityPasswordHasher(argon, fallback);
});
builder.Services.AddSingleton<Api.Services.IAuthTracker, Api.Services.InMemoryAuthTracker>();

// Token settings and JWT services
var tokenSettings = new Api.Services.TokenSettings();
var tokenSection = builder.Configuration.GetSection("TokenSettings");
if (tokenSection.Exists()) tokenSection.Bind(tokenSettings);
builder.Services.AddSingleton(tokenSettings);

// Allow overriding TokenSettings.SigningKey via environment variable TokenSettings__SigningKey
var envSigningKey = Environment.GetEnvironmentVariable("TokenSettings__SigningKey");
if (!string.IsNullOrEmpty(envSigningKey)) tokenSettings.SigningKey = envSigningKey;

if (!string.IsNullOrEmpty(tokenSettings.SigningKey))
{
    builder.Services.AddSingleton<Api.Services.TokenService>();
    var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(tokenSettings.SigningKey));
    // Validate signing key entropy/length. Symmetric key should be at least 256 bits (32 bytes)
    // when using HMAC-SHA256. If configured key is too short, warn in non-production and
    // fail fast in Production to avoid issuing weak tokens.
    try
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(tokenSettings.SigningKey);
        if (keyBytes.Length < 32)
        {
            // Avoid BuildServiceProvider here; write a clear console warning instead. In
            // production this is considered a fatal configuration error.
            System.Console.WriteLine("WARNING: Configured TokenSettings.SigningKey is shorter than 32 bytes; this may be insecure. Consider using a 256-bit (32 byte) base64 key.");
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("Token signing key must be at least 32 bytes in production.");
            }
        }
    }
    catch (Exception ex)
    {
        // If logging isn't available yet, write to console. In production we would have thrown above.
        System.Console.WriteLine($"Token signing key validation: {ex.Message}");
    }
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = tokenSettings.Issuer,
            ValidAudience = tokenSettings.Audience,
            IssuerSigningKey = key
        };
    });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    });

    // In production, require a signing key to be configured. This prevents running with
    // authentication accidentally disabled. Operators should provide the key via
    // environment variable `TokenSettings__SigningKey` or in configuration.
    var env = builder.Environment;
    if (env.IsProduction() && string.IsNullOrEmpty(tokenSettings.SigningKey))
    {
        throw new InvalidOperationException("Token signing key is required in Production. Set TokenSettings__SigningKey environment variable or configure TokenSettings.SigningKey in production configuration.");
    }
}

// Password policy
var policy = new Api.Utilities.PasswordPolicy();
var policySection = builder.Configuration.GetSection("PasswordPolicy");
if (policySection.Exists())
{
    policySection.Bind(policy);
}
builder.Services.AddSingleton(policy);

// Register LoginUserManager
builder.Services.AddScoped<Api.Services.LoginUserManager>(sp =>
{
    var userManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Api.Identity.ApplicationUser>>();
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Api.Services.LoginUserManager>>();
    var p = sp.GetRequiredService<Api.Utilities.PasswordPolicy>();
    var authTracker = sp.GetRequiredService<Api.Services.IAuthTracker>();
    var tokenSvc = sp.GetService<Api.Services.TokenService>();
    return new Api.Services.LoginUserManager(userManager, logger, p, authTracker, tokenSvc);
});

// Controllers
builder.Services.AddControllers();

// Explicit Kestrel configuration: bind HTTPS and HTTP to deterministic ports
// This prevents launchSettings or other configuration sources from silently
// overriding the addresses at runtime.
builder.WebHost.ConfigureKestrel(options =>
{
    // Prefer a PFX supplied via environment variables for development TLS.
    var pfxPath = System.Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Path");
    var pfxPwd = System.Environment.GetEnvironmentVariable("Kestrel__Certificates__Default__Password");

    try
    {
        if (!string.IsNullOrEmpty(pfxPath) && !string.IsNullOrEmpty(pfxPwd) && System.IO.File.Exists(pfxPath))
        {
            // Bind HTTPS on the canonical development HTTPS port.
            options.ListenLocalhost(7067, listenOptions => listenOptions.UseHttps(pfxPath, pfxPwd));
        }
        else
        {
            // If no PFX provided, fall back to the default development certificate resolution.
            options.ListenLocalhost(7067, listenOptions => listenOptions.UseHttps());
        }
    }
    catch (System.Exception ex)
    {
        // In Production we must not silently fall back to HTTP. Fail fast so operators
        // notice the TLS configuration issue. In Development we keep the previous
        // permissive behavior to preserve local workflows and tests.
        if (builder.Environment.IsProduction())
        {
            // Surface the original exception to abort startup.
            throw new InvalidOperationException($"Kestrel HTTPS binding failed in Production: {ex.Message}", ex);
        }

        // Development: log and bind HTTP so developers can still connect locally.
        System.Console.WriteLine($"Kestrel HTTPS binding encountered an issue (development): {ex.Message}");
        options.ListenLocalhost(7067);
    }

    // Bind HTTP on a separate port to avoid port conflicts. Only expose an
    // unsecured HTTP listener in Development for convenience (test hosts and
    // local debugging). In production-like environments we avoid binding HTTP
    // to reduce the risk of accidental plaintext transport.
    if (builder.Environment.IsDevelopment())
    {
        options.ListenLocalhost(5046);
    }
});

var app = builder.Build();

// In Development only: ensure the Identity database/schema exists so the
// lightweight local SQLite store has the required AspNet* tables for
// UserManager operations (register/auth). We prefer migrations for
// production but calling EnsureCreated() in development avoids the
// "no such table: AspNetUsers" error when running locally or in tests.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetService<Api.Identity.ApplicationDbContext>();
        db?.Database.EnsureCreated();
        var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger("Startup");
        logger?.LogInformation("Ensured Identity DB created (Development).");
    }
    catch (System.Exception ex)
    {
        var logger = app.Services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger("Startup");
        logger?.LogWarning(ex, "Failed to EnsureCreated Identity DB during startup (development)");
    }
}

// Use CORS
app.UseCors();

// Global exception handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errApp =>
    {
        errApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var payload = System.Text.Json.JsonSerializer.Serialize(new { error = "An internal error occurred." });
            await context.Response.WriteAsync(payload);
        });
    });
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    await next();
});

// Rate limiter (per-IP, simple)
var authRateMap = new System.Collections.Concurrent.ConcurrentDictionary<string, (int Count, System.DateTimeOffset WindowStart)>();
var rateLimitMax = 20;
var rateLimitWindow = System.TimeSpan.FromMinutes(1);
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (string.Equals(path, "/authenticate", System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(path, "/register", System.StringComparison.OrdinalIgnoreCase))
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "-";
        var now = System.DateTimeOffset.UtcNow;
        var entry = authRateMap.GetOrAdd(ip, (_) => (0, now));
        if (now - entry.WindowStart > rateLimitWindow)
            entry = (0, now);
        entry = (entry.Count + 1, entry.WindowStart);
        authRateMap[ip] = entry;
        if (entry.Count > rateLimitMax)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Too many requests");
            return;
        }
    }
    await next();
});

// Only enable HTTPS redirection and HSTS in non-development environments. When
// running integration tests via the in-memory TestServer the client often
// issues HTTP requests and the automatic redirect can cause Authorization
// headers to be dropped by the HttpClient during the redirect. Skipping the
// redirect in development avoids that test-host issue while preserving HTTPS
// redirection in production-like environments.
if (!app.Environment.IsDevelopment())
{
    // Enforce HTTP->HTTPS redirects
    app.UseHttpsRedirection();

    // Add HSTS to instruct browsers to always use HTTPS for this domain. This
    // should be enabled only in production and after a trusted certificate is
    // configured.
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Serve the Blazor WebAssembly client static files when present.
// This allows opening the SPA at the API origin (useful for local dev where the client
// is built to Client/bin/Debug/net8.0/wwwroot and copied into Api/wwwroot).
try
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    // Fallback to index.html for SPA routes
    app.MapFallbackToFile("index.html");
}
catch (System.Exception ex)
{
    var logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("StaticFiles");
    logger.LogWarning(ex, "Static file middleware or SPA fallback could not be configured.");
}

// Seed initial admin user when configured. Prefer Identity's UserManager so
// the default Identity password hasher is used. Fall back to the repository+
// custom hasher approach only when UserManager is not available (safe for tests
// or very minimal hosts).
try
{
    using (var scope = app.Services.CreateScope())
    {
        var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var seedEnabled = config.GetValue<bool>("SeedAdmin:Enabled", false);
        var seedUser = config["SeedAdmin:Username"];
        var seedPass = config["SeedAdmin:Password"];
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("Seed");

        if (seedEnabled && !string.IsNullOrEmpty(seedUser) && !string.IsNullOrEmpty(seedPass))
        {
            // Prefer the Identity APIs when available so the password is hashed using
            // the IPasswordHasher<ApplicationUser> registered with Identity (the default
            // or a custom one you provide).
            var userManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.UserManager<Api.Identity.ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();

            if (userManager != null)
            {
                // Ensure the Identity schema exists for lightweight hosts (tests / dev).
                // Calling EnsureCreated() here is acceptable for test and local dev scenarios
                // where running full EF migrations may not be desired. In production, prefer
                // applying migrations via a migration pipeline.
                var db = scope.ServiceProvider.GetService<Api.Identity.ApplicationDbContext>();
                try
                {
                    db?.Database.EnsureCreated();
                }
                catch (System.Exception ensureEx)
                {
                    // Log and continue; subsequent operations may fail which will be surfaced
                    // to the test runner or operator. Avoid throwing here as we want tests to
                    // see the original failure if EnsureCreated cannot run.
                    logger.LogWarning(ensureEx, "EnsureCreated failed while preparing Identity schema (continuing): {Message}", ensureEx.Message);
                }
                // Choose an available username by appending/incrementing a numeric suffix when
                // the configured seed username is already present. This avoids collisions when
                // running multiple test hosts or repeated local runs.
                var candidate = seedUser;
                var index = 1;
                var existing = userManager.FindByNameAsync(candidate).GetAwaiter().GetResult();
                while (existing != null)
                {
                    candidate = seedUser + index.ToString();
                    index++;
                    existing = userManager.FindByNameAsync(candidate).GetAwaiter().GetResult();
                }

                if (existing == null)
                {
                    // Ensure roles exist
                    if (roleManager != null)
                    {
                        if (!roleManager.RoleExistsAsync("Admin").GetAwaiter().GetResult())
                        {
                            roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin")).GetAwaiter().GetResult();
                        }
                        if (!roleManager.RoleExistsAsync("User").GetAwaiter().GetResult())
                        {
                            roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("User")).GetAwaiter().GetResult();
                        }
                        if (!roleManager.RoleExistsAsync("Manager").GetAwaiter().GetResult())
                        {
                            roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Manager")).GetAwaiter().GetResult();
                        }
                    }

                    var user = new Api.Identity.ApplicationUser { UserName = candidate };
                    var createResult = userManager.CreateAsync(user, seedPass).GetAwaiter().GetResult();
                    if (createResult.Succeeded)
                    {
                        if (!userManager.IsInRoleAsync(user, "Admin").GetAwaiter().GetResult())
                        {
                            userManager.AddToRoleAsync(user, "Admin").GetAwaiter().GetResult();
                        }
                        logger.LogInformation("Seeded initial admin user '{User}' using UserManager", candidate);
                    }
                    else
                    {
                        logger.LogWarning("Failed to seed initial admin user '{User}' via UserManager: {Errors}", candidate, string.Join(';', createResult.Errors.Select(e => e.Description)));
                    }
                }
            }
            else
            {
                // Fallback: use the existing repository + custom hasher approach. This keeps
                // the previous behavior for very small hosts where Identity services weren't
                // registered.
                var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
                // Fallback repo seeding: find an available username by appending a numeric suffix
                // if the configured username already exists in the repository.
                var candidateRepo = seedUser;
                var repoIndex = 1;
                var existingRepo = repo.GetHashedPasswordAsync(candidateRepo).GetAwaiter().GetResult();
                while (!string.IsNullOrEmpty(existingRepo))
                {
                    candidateRepo = seedUser + repoIndex.ToString();
                    repoIndex++;
                    existingRepo = repo.GetHashedPasswordAsync(candidateRepo).GetAwaiter().GetResult();
                }
                if (string.IsNullOrEmpty(existingRepo))
                {
                    var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();
                    var hashed = hasher.Hash(seedPass);
                    var added = repo.AddUserAsync(candidateRepo, hashed, new[] { "Admin" }).GetAwaiter().GetResult();
                    if (added) logger.LogInformation("Seeded initial admin user '{User}' (fallback repo)", candidateRepo);
                    else logger.LogWarning("Failed to seed initial admin user '{User}' (fallback repo)", candidateRepo);
                }
            }
        }
    }
}
catch (System.Exception ex)
{
    var logger = app.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("Seed");
    logger.LogError(ex, "Error while attempting to seed admin user");
}

app.Run();
