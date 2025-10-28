using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("admin")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger<AdminController> _logger;

        public AdminController(Microsoft.Extensions.Logging.ILogger<AdminController> logger)
        {
            _logger = logger;
        }
        [HttpGet("status")]
        public IActionResult Status()
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { status = "ok" });
            return Content(payload, "application/json");
        }

        [HttpGet("users/{username}/roles")]
        public async Task<IActionResult> GetRoles(string username, [FromServices] Api.Repositories.IUserRepository repo)
        {
            var caller = User?.Identity?.Name ?? "(unknown)";
            _logger.LogInformation("GetRoles invoked by '{Caller}' for user '{Target}'", caller, username);
            var roles = await repo.GetRolesAsync(username);
            if (roles == null) return NotFound();
            var payload = System.Text.Json.JsonSerializer.Serialize(new { username, roles });
            return Content(payload, "application/json");
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromServices] Api.Repositories.IUserRepository repo)
        {
            var caller = User?.Identity?.Name ?? "(unknown)";
            _logger.LogInformation("GetAllUsers invoked by '{Caller}'", caller);
            var map = await repo.GetAllUsersAsync();
            var payload = System.Text.Json.JsonSerializer.Serialize(map);
            return Content(payload, "application/json");
        }

        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles([FromServices] Api.Repositories.IUserRepository repo)
        {
            var caller = User?.Identity?.Name ?? "(unknown)";
            _logger.LogInformation("GetAllRoles invoked by '{Caller}'", caller);
            // Return the canonical allowed roles for the application rather than
            // dynamically computing roles based on currently assigned roles in the repo.
            // This ensures UI shows all possible roles (Admin, User, Manager) even when
            // no users currently have those roles.
            var roles = Api.Utilities.Roles.All;
            var payload = System.Text.Json.JsonSerializer.Serialize(new { roles });
            return Content(payload, "application/json");
        }

        [HttpGet("password-policy")]
        public IActionResult GetPasswordPolicy([FromServices] Api.Utilities.PasswordPolicy policy)
        {
            if (policy == null) return NotFound();
            var payload = System.Text.Json.JsonSerializer.Serialize(new { minLength = policy.MinLength, maxLength = policy.MaxLength });
            return Content(payload, "application/json");
        }

        [HttpPost("users/{username}/roles")]
        public async Task<IActionResult> SetRoles(string username, [FromBody] System.Collections.Generic.Dictionary<string, string[]> body, [FromServices] Api.Repositories.IUserRepository repo)
        {
            var caller = User?.Identity?.Name ?? "(unknown)";
            _logger.LogInformation("SetRoles invoked by '{Caller}' for user '{Target}' with roles: {Roles}", caller, username, body == null ? "(null)" : string.Join(',', body.TryGetValue("roles", out var r) ? r : System.Array.Empty<string>()));
            if (body == null || !body.TryGetValue("roles", out var roles)) return BadRequest();
            var ok = await repo.SetRolesAsync(username, roles);
            if (!ok) return NotFound();
            var resp = System.Text.Json.JsonSerializer.Serialize(new { username, roles });
            return Content(resp, "application/json");
        }

        // POST /admin/users
        // Body: { "username":"...", "password":"...", "roles":["Role1","Role2"] }
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] Api.Dtos.AdminCreateUserRequest req,
            [FromServices] Api.Repositories.IUserRepository repo,
            [FromServices] Api.Utilities.IPasswordHasher hasher,
            [FromServices] Api.Utilities.PasswordPolicy policy)
        {
            try
            {
                var caller = User?.Identity?.Name ?? "(unknown)";
                _logger.LogInformation("CreateUser invoked by '{Caller}' for username '{Username}'", caller, req?.Username);
            if (req == null)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "invalid request" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
            }

            // Defensive null-checks for injected services when called directly in unit tests
            if (repo == null)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "user repository unavailable" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 500 };
            }
            if (hasher == null)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "password hasher unavailable" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 500 };
            }
            if (policy == null)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "password policy unavailable" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 500 };
            }

            // Use strict username sanitizer for identifiers to avoid surprises
            var username = Api.Utilities.ValidationHelpers.SanitizeUsername(req.Username ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(username))
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "username is required" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
            }

            var password = req.Password ?? string.Empty;
            if (!policy.IsSatisfiedBy(password))
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "password does not meet policy" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
            }

            var existing = await repo.GetHashedPasswordAsync(username);
            if (!string.IsNullOrEmpty(existing))
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "user exists" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 409 };
            }

            var hashed = hasher.Hash(password);
            // Ensure at least one role is present; default to "User" if none provided.
            var roles = (req.Roles == null || req.Roles.Length == 0)
                ? new[] { Api.Utilities.Roles.User }
                : req.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => Api.Utilities.ValidationHelpers.Sanitize(r).Trim()).Where(r => !string.IsNullOrEmpty(r)).ToArray();

            if (roles.Length == 0) roles = new[] { "User" };

            // Validate roles against allowed list
            var allowed = Api.Utilities.Roles.All;
            foreach (var r in roles)
            {
                if (!allowed.Contains(r, System.StringComparer.OrdinalIgnoreCase))
                {
                    var err = System.Text.Json.JsonSerializer.Serialize(new { error = $"role '{r}' is not allowed" });
                    return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 400 };
                }
            }

            var added = await repo.AddUserAsync(username, hashed, roles);
            if (added)
                _logger.LogInformation("User '{Username}' created by '{Caller}' with roles: {Roles}", username, caller, string.Join(',', roles));
            else
                _logger.LogWarning("Failed to create user '{Username}' attempted by '{Caller}'", username, caller);
            if (!added) return StatusCode(500);

            var resp = System.Text.Json.JsonSerializer.Serialize(new { username, roles });
            // Return 201 with Location header and JSON payload using Content to avoid
            // relying on the framework output formatter (some test hosts' PipeWriter
            // implementations do not support UnflushedBytes). Set Location header
            // and return the JSON payload explicitly.
            if (HttpContext?.Response?.Headers != null)
            {
                HttpContext.Response.Headers["Location"] = $"/admin/users/{username}/roles";
            }
            return new ContentResult { Content = resp, ContentType = "application/json", StatusCode = 201 };
            }
            catch (System.Exception ex)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "internal", detail = ex.ToString() });
                // Log full exception for diagnostics
                _logger?.LogError(ex, "Unhandled exception in CreateUser");
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 500 };
            }
        }

        [HttpGet("users/{username}/manager")]
        public async Task<IActionResult> GetManager(string username, [FromServices] Api.Repositories.IUserRepository repo)
        {
            // Return { manager: "username" } or { manager: null }
            var usersForManager = await repo.GetUsersForManagerAsync(username);
            // repo has no direct GetManagerOfUser; InMemoryUserRepository stores reverse map, so use GetRoles to verify existence
            var roles = await repo.GetRolesAsync(username);
            if (roles == null) return NotFound();

            // Try to determine manager by looking up all users and finding who has this username assigned.
            // This is O(n) but acceptable for small in-memory repo. SQL-backed repo may override with efficient query.
            string? manager = null;
            // Iterate all users and check manager mapping via GetUsersForManagerAsync: find any manager whose list contains this username
            var all = await repo.GetAllUsersAsync();
            foreach (var kv in all)
            {
                var candidate = kv.Key;
                var managed = await repo.GetUsersForManagerAsync(candidate);
                if (managed != null && System.Array.IndexOf(managed, username) >= 0)
                {
                    manager = candidate;
                    break;
                }
            }

            var payload = System.Text.Json.JsonSerializer.Serialize(new { username, manager });
            return Content(payload, "application/json");
        }

        [HttpPost("users/{username}/manager")]
        public async Task<IActionResult> SetManager(string username, [FromBody] System.Collections.Generic.Dictionary<string, string?> body, [FromServices] Api.Repositories.IUserRepository repo)
        {
            if (body == null) return BadRequest();
            body.TryGetValue("manager", out var manager);

            // If manager is null or empty, clear manager assignment
            if (string.IsNullOrWhiteSpace(manager)) manager = null;

            var ok = await repo.SetManagerAsync(username, manager ?? string.Empty);
            if (!ok)
            {
                var err = System.Text.Json.JsonSerializer.Serialize(new { error = "user not found or update failed" });
                return new ContentResult { Content = err, ContentType = "application/json", StatusCode = 404 };
            }
            var payload = System.Text.Json.JsonSerializer.Serialize(new { username, manager });
            return Content(payload, "application/json");
        }
    }
}
