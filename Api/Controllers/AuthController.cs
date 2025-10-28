using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    // Keep these routes at the application root so existing clients/tests can POST to
    // /register and /authenticate (previously were minimal endpoints).
    [Route("")]
    public class AuthController : ControllerBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger<AuthController> _logger;

        public AuthController(Microsoft.Extensions.Logging.ILogger<AuthController> logger)
        {
            _logger = logger;
        }
    [HttpPost("register")]
    [HttpPost("api/register")]
    public async Task<IActionResult> Register([FromBody] Api.Dtos.RegisterRequest req, [FromServices] Api.Services.LoginUserManager manager)
        {
            if (req == null)
            {
                var errPayload = System.Text.Json.JsonSerializer.Serialize(new { error = "Invalid payload" });
                return new ContentResult { Content = errPayload, ContentType = "application/json", StatusCode = 400 };
            }

            _logger.LogInformation("Register attempt for username '{Username}'", req.Username);
            var user = new Api.Models.LoginUser { Username = req.Username, Password = req.Password };
            var success = await manager.RegisterAsync(user);
            if (success)
            {
                _logger.LogInformation("Registration succeeded for username '{Username}'", req.Username);
                var payload = System.Text.Json.JsonSerializer.Serialize(new { success = true });
                return Content(payload, "application/json");
            }

            _logger.LogWarning("Registration failed for username '{Username}'", req.Username);
            var fail = System.Text.Json.JsonSerializer.Serialize(new { success = false, error = "registration failed" });
            return new ContentResult { Content = fail, ContentType = "application/json", StatusCode = 400 };
        }

    [HttpPost("authenticate")]
    [HttpPost("api/authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] Api.Dtos.AuthenticateRequest req, [FromServices] Api.Services.LoginUserManager manager)
        {
            if (req == null) return BadRequest("Invalid payload");
            _logger.LogInformation("Authentication attempt for username '{Username}'", req.Username);
            var token = await manager.AuthenticateWithTokenAsync(req.Username, req.Password);
            if (token != null)
            {
                // Avoid using the framework JSON output formatter in the test host which can
                // throw when the PipeWriter doesn't support UnflushedBytes. Manually serialize
                // the response and return as content to be robust in test environments.
                _logger.LogInformation("Authentication successful for username '{Username}' - issuing token", req.Username);
                var payload = System.Text.Json.JsonSerializer.Serialize(new { token });
                return Content(payload, "application/json");
            }
            var authenticated = await manager.AuthenticateAsync(req.Username, req.Password);
            if (authenticated)
            {
                _logger.LogInformation("Authentication succeeded for username '{Username}'", req.Username);
                var okPayload = System.Text.Json.JsonSerializer.Serialize(new { success = true });
                return new ContentResult { Content = okPayload, ContentType = "application/json", StatusCode = 200 };
            }
            _logger.LogWarning("Authentication failed for username '{Username}'", req.Username);
            var unauth = System.Text.Json.JsonSerializer.Serialize(new { success = false, error = "invalid credentials" });
            return new ContentResult { Content = unauth, ContentType = "application/json", StatusCode = 401 };
        }
    }
}
