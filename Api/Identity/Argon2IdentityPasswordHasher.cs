using System;
using Microsoft.AspNetCore.Identity;
using Api.Utilities;

namespace Api.Identity
{
    /// <summary>
    /// Identity-compatible password hasher that delegates Argon2 operations to the
    /// project's <see cref="Api.Utilities.IPasswordHasher"/> implementation, and
    /// falls back to the default Identity <see cref="PasswordHasher{TUser}"/>
    /// for legacy formats. When a legacy hash verifies successfully this adapter
    /// returns SuccessRehashNeeded so callers can re-hash to Argon2.
    /// </summary>
    public class Argon2IdentityPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly IPasswordHasher _argon;
        private readonly PasswordHasher<ApplicationUser> _fallback;

        public Argon2IdentityPasswordHasher(IPasswordHasher argon, PasswordHasher<ApplicationUser>? fallback = null)
        {
            _argon = argon ?? throw new ArgumentNullException(nameof(argon));
            _fallback = fallback ?? new PasswordHasher<ApplicationUser>();
        }

        public string HashPassword(ApplicationUser user, string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            return _argon.Hash(password);
        }

        public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
                return PasswordVerificationResult.Failed;

            // Detect Argon2 PHC-style strings and verify via the project's Argon2 hasher
            if (hashedPassword.StartsWith("$argon2id$", StringComparison.Ordinal))
            {
                var ok = _argon.Verify(providedPassword, hashedPassword);
                if (!ok) return PasswordVerificationResult.Failed;
                return _argon.NeedsUpgrade(hashedPassword) ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Success;
            }

            // Not Argon2 — fallback to the default Identity hasher (likely PBKDF2).
            // If verification succeeds, indicate a rehash is needed so we can migrate
            // the stored hash to Argon2 on the next successful login.
            var res = _fallback.VerifyHashedPassword(user, hashedPassword, providedPassword);
            if (res == PasswordVerificationResult.Success) return PasswordVerificationResult.SuccessRehashNeeded;
            return res;
        }
    }
}
