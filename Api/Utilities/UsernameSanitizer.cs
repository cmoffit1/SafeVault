using System.Text.RegularExpressions;

namespace Api.Utilities
{
    public static class UsernameSanitizer
    {
        /// <summary>
        /// Sanitizes usernames by applying a strict allow-list. Removes any characters
        /// not in the approved set and lower-cases the result.
        /// </summary>
        public static string SanitizeUsername(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input.Normalize(System.Text.NormalizationForm.FormKC).Trim();
            s = Regex.Replace(s, "\\p{C}+", string.Empty);
            // Allow letters, digits, dot, underscore, at. Remove everything else.
            s = Regex.Replace(s, "[^A-Za-z0-9._@]+", string.Empty);
            return s.ToLowerInvariant();
        }
    }
}
