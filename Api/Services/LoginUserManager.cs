using System.Threading.Tasks;
using Api.Models;
using Api.Repositories;
using Api.Utilities;
using Api.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Api.Services
{
    public class LoginUserManager
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthTracker _authTracker;
        private readonly Utilities.PasswordPolicy _policy;
        private readonly ILogger<LoginUserManager> _logger;
        private readonly Api.Services.TokenService? _tokenService;

        public LoginUserManager(UserManager<ApplicationUser> userManager, ILogger<LoginUserManager>? logger = null, Utilities.PasswordPolicy? policy = null, IAuthTracker? authTracker = null, Api.Services.TokenService? tokenService = null)
        {
            _userManager = userManager;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LoginUserManager>.Instance;
            _policy = policy ?? new Utilities.PasswordPolicy();
            _authTracker = authTracker ?? new InMemoryAuthTracker();
            _tokenService = tokenService;
        }

        public async Task<bool> RegisterAsync(LoginUser user)
        {
            if (user == null)
            {
                _logger.LogWarning("RegisterAsync called with null user");
                return false;
            }

            var sanitized = user.Sanitized();
            if (!sanitized.IsValid())
            {
                _logger.LogInformation("Registration failed: input validation failed for username '{Username}'", sanitized.Username);
                return false;
            }

            const int usernameMin = 3, usernameMax = 256;
            if (sanitized.Username == null)
            {
                _logger.LogInformation("Registration failed: empty username after sanitization");
                return false;
            }

            var normalizedUsername = Utilities.UsernameSanitizer.SanitizeUsername(sanitized.Username);
            if (normalizedUsername.Length < usernameMin || normalizedUsername.Length > usernameMax)
            {
                _logger.LogInformation("Registration failed: username '{Username}' length {Length} outside allowed range", normalizedUsername, normalizedUsername.Length);
                return false;
            }

            if (!_policy.IsSatisfiedBy(sanitized.Password))
            {
                _logger.LogInformation("Registration failed: password policy not satisfied for username '{Username}'", normalizedUsername);
                return false;
            }

            // Prevent duplicate registrations early. Some IUserStore implementations
            // (notably lightweight test stores) may not enforce uniqueness, so check
            // using UserManager which will normalize the username consistently.
            var existing = await _userManager.FindByNameAsync(normalizedUsername);
            if (existing != null)
            {
                _logger.LogWarning("Registration failed: username '{Username}' already exists", normalizedUsername);
                return false;
            }

            var userEntity = new ApplicationUser { UserName = normalizedUsername };
            var createResult = await _userManager.CreateAsync(userEntity, sanitized.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("Registration failed for '{Username}': {Errors}", normalizedUsername, string.Join(';', createResult.Errors.Select(e => e.Description)));
                return false;
            }

            if (!await _userManager.IsInRoleAsync(userEntity, "User"))
            {
                await _userManager.AddToRoleAsync(userEntity, "User");
            }

            _logger.LogInformation("User '{Username}' registered successfully", normalizedUsername);
            return true;
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                _logger.LogInformation("Authentication failed: empty username or password");
                return false;
            }

            var sanitizedUsername = Utilities.UsernameSanitizer.SanitizeUsername(username);
            _logger.LogInformation("Authentication attempt for username '{Username}'", sanitizedUsername);
            if (await _authTracker.IsLockedOutAsync(sanitizedUsername))
            {
                _logger.LogWarning("Authentication attempt for locked-out username '{Username}'", sanitizedUsername);
                return false;
            }

            var user = await _userManager.FindByNameAsync(sanitizedUsername);
            if (user == null)
            {
                _logger.LogInformation("Authentication failed: no such user '{Username}'", sanitizedUsername);
                await _authTracker.RecordFailureAsync(sanitizedUsername);
                return false;
            }

            var ok = await _userManager.CheckPasswordAsync(user, password);
            if (ok)
            {
                _logger.LogInformation("Authentication succeeded for username '{Username}'", sanitizedUsername);
                await _authTracker.ResetAsync(sanitizedUsername);

                var hasher = _userManager.PasswordHasher;
                var verify = hasher.VerifyHashedPassword(user, user.PasswordHash ?? string.Empty, password);
                if (verify == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.PasswordHash = hasher.HashPassword(user, password);
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("Rehashed password for username '{Username}' with upgraded parameters", sanitizedUsername);
                }
            }
            else
            {
                _logger.LogInformation("Authentication failed: invalid credentials for username '{Username}'", sanitizedUsername);
                var failures = await _authTracker.RecordFailureAsync(sanitizedUsername);
                if (failures > 0)
                    _logger.LogWarning("Failed login count {Count} for username '{Username}'", failures, sanitizedUsername);
            }

            return ok;
        }

        public async Task<string?> AuthenticateWithTokenAsync(string username, string password)
        {
            var ok = await AuthenticateAsync(username, password);
            if (!ok) return null;

            if (_tokenService == null)
            {
                _logger.LogWarning("Token service not configured; cannot issue token for '{Username}'", username);
                return null;
            }

            var sanitizedUsername = Utilities.UsernameSanitizer.SanitizeUsername(username);
            var user = await _userManager.FindByNameAsync(sanitizedUsername);
            if (user == null) return null;
            var roles = (await _userManager.GetRolesAsync(user)).ToArray();
            var token = _tokenService.CreateToken(sanitizedUsername, roles);
            return token;
        }
    }
}