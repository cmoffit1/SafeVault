using Microsoft.Data.SqlClient;

namespace Api.Models
{
    public class LoginUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        // Returns a sanitized copy of this user (does not mutate original)
        public LoginUser Sanitized()
        {
            return new LoginUser
            {
                // Use the strict username sanitizer for identifiers
                Username = Utilities.UsernameSanitizer.SanitizeUsername(Username) ?? string.Empty,
                // Do NOT run SQL/keyword sanitization over passwords — that can mangle normal passwords
                // (for example removing "or" from "correct12"). Keep the password as-provided, trimmed.
                Password = (Password ?? string.Empty).Trim()
            };
        }

        // Validate using ValidationHelpers.IsValidInput with conservative allowed chars
        public bool IsValid()
        {
            // Allow common username chars and a small set for passwords
            var usernameAllowed = "._@"; // allow dot, underscore, at
            var passwordAllowed = "!@#$%^&*()-_=+[]{}|;:,.?~`";

            // Basic checks: non-empty and allowed characters. Length requirements are handled elsewhere.
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
                return false;

            if (!Utilities.InputNormalizer.IsValidInput(Username, usernameAllowed)
                || !Utilities.InputNormalizer.IsValidInput(Password, passwordAllowed))
                return false;

            // IsValid should not perform DB access. Authentication/registration logic
            // that needs the database should be performed in services/repositories.
            return true;
        }
    }
}