using System.Text.RegularExpressions;

namespace Api.Utilities
{
    public static class InputNormalizer
    {
        /// <summary>
        /// Normalize and conservatively sanitize free text input: trim, normalize Unicode,
        /// strip control characters and collapse excessive whitespace.
        /// </summary>
        public static string NormalizeText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var s = input.Normalize(System.Text.NormalizationForm.FormKC).Trim();
            s = Regex.Replace(s, "\\p{C}+", string.Empty);
            s = Regex.Replace(s, "\\s+", " ");
            return s;
        }

        /// <summary>
        /// Generic allow-list validator: checks that input contains only letters/digits/whitespace
        /// or allowed special characters.
        /// </summary>
        public static bool IsValidInput(string input, string allowedSpecialChars = "")
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var validChars = allowedSpecialChars.ToHashSet();
            return input.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || validChars.Contains(c));
        }
    }
}
