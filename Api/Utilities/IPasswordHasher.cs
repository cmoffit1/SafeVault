namespace Api.Utilities
{
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashes the provided password and returns a versioned string that contains
        /// salt and derived key suitable for storage.
        /// </summary>
        /// <param name="password">The plain-text password to hash.</param>
        string Hash(string password);

        /// <summary>
        /// Verifies whether the provided plain-text password matches the stored
        /// hashed representation.
        /// </summary>
        /// <param name="password">The plain-text password to verify.</param>
        /// <param name="hashed">The stored hashed blob previously returned by <see cref="Hash"/>.</param>
        bool Verify(string password, string hashed);

        /// <summary>
        /// Returns true when the stored hash is considered weaker than current
        /// configuration and should be re-hashed with current parameters.
        /// </summary>
        bool NeedsUpgrade(string hashed);
    }
}
