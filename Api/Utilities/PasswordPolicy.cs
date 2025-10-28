namespace Api.Utilities
{
    public class PasswordPolicy
    {
        /// <summary>
        /// Minimum allowed password length.
        /// </summary>
    // Raised default minimum to 12 to discourage short passwords; still configurable
    // via configuration to allow flexibility for different deployments.
    public int MinLength { get; set; } = 12;

        /// <summary>
        /// Maximum allowed password length.
        /// </summary>
        public int MaxLength { get; set; } = 512;

        /// <summary>
        /// Returns true when the supplied password satisfies the configured length
        /// bounds. This method intentionally checks only length; additional
        /// complexity rules may be layered on top by the caller.
        /// </summary>
        public bool IsSatisfiedBy(string? password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            return password.Length >= MinLength && password.Length <= MaxLength;
        }
    }
}
