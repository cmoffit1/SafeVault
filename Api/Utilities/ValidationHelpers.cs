using System.Text.RegularExpressions;

namespace Api.Utilities
{
    public static class ValidationHelpers
    {
        // NOTE: Removing SQL keywords from arbitrary input is unsafe because it can mangle
        // legitimate data (for example removing "or" from a password). Instead, prefer
        // whitelisting (allow-list) and proper parameterized queries. Provide a
        // conservative sanitizer for usernames and a normalization helper for general inputs.

        /// <summary>
        /// Normalize and conservatively sanitize free text input: trim, normalize Unicode,
        /// strip control characters and collapse excessive whitespace. Do NOT remove SQL
        /// keywords here — use parameterized queries and allow-listing for identifiers.
        /// </summary>
        public static string Sanitize(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Normalize to NFC/NFKC to avoid tricky Unicode variants
            var s = input.Normalize(System.Text.NormalizationForm.FormKC).Trim();

            // Remove control characters
            s = Regex.Replace(s, "\\p{C}+", string.Empty);

            // Collapse multiple whitespace characters into a single space
            s = Regex.Replace(s, "\\s+", " ");

            return s;
        }

        /// <summary>
        /// Sanitizes usernames by applying a strict allow-list. Removes any characters
        /// not in the approved set and lower-cases the result. This is suitable for
        /// identifiers that will be used as keys in storage and comparisons.
        /// </summary>
        public static string SanitizeUsername(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input.Normalize(System.Text.NormalizationForm.FormKC).Trim();

            // Remove control characters
            s = Regex.Replace(s, "\\p{C}+", string.Empty);

            // Allow only letters, digits and a small set of safe punctuation for usernames
            // (dot, underscore, at). Remove everything else (note: hyphen removed to avoid
            // preserving SQL comment markers like '--' that can make normalization inconsistent).
            s = Regex.Replace(s, "[^A-Za-z0-9._@]+", string.Empty);

            return s.ToLowerInvariant();
        }

        public static bool IsValidInput(string input, string allowedSpecialChars = "")
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var validChars = allowedSpecialChars.ToHashSet();
            return input.All(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || validChars.Contains(c));

        }

        /// <summary>
        /// Perform a conservative sanitization suitable for rendering untrusted text into HTML.
        /// This removes script tags, event handler attributes, javascript: URIs, and strips angle brackets.
        /// It is intentionally conservative: it removes any angle brackets to avoid allowing markup.
        /// </summary>
        public static string SanitizeForHtml(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input;

            // Remove null bytes
            s = s.Replace("\0", string.Empty);

            // Remove script tags and their content
            s = Regex.Replace(s, @"<script.*?>.*?</script>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Remove any on* attributes (onload, onclick, etc.) - double and single quoted
            s = Regex.Replace(s, @"on[A-Za-z]+\s*=\s*""[^"" ]*""", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"on[A-Za-z]+\s*=\s*'[^']*'", string.Empty, RegexOptions.IgnoreCase);

            // Remove javascript: URIs (simple removal of the scheme)
            s = Regex.Replace(s, @"javascript:\s*", string.Empty, RegexOptions.IgnoreCase);

            // Remove data: URIs that could embed scripts (e.g., data:text/html;...)
            s = Regex.Replace(s, @"data:\s*text/html[^\s>]*", string.Empty, RegexOptions.IgnoreCase);

            // Strip angle brackets to avoid any remaining tags
            s = s.Replace("<", string.Empty).Replace(">", string.Empty);

            // Optionally escape remaining HTML entities (helpful when rendering)
            s = System.Net.WebUtility.HtmlEncode(s);

            return s;
        }

        /// <summary>
        /// Heuristic check for likely XSS attempts. Returns true when patterns commonly used in XSS are found.
        /// This is a detection helper and should not replace proper sanitization/encoding.
        /// </summary>
        public static bool IsLikelyXssAttempt(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input;

            // Look for script tags, event handler attributes, javascript: or data: URIs
            if (Regex.IsMatch(s, @"(?i)<script\b")) return true;
            if (Regex.IsMatch(s, @"(?i)on[a-z]+\s*=")) return true;
            if (Regex.IsMatch(s, @"(?i)javascript:\s*")) return true;
            if (Regex.IsMatch(s, @"data:\s*text/html", RegexOptions.IgnoreCase)) return true;

            // look for angle brackets with tag-like content
            if (s.Contains('<') || s.Contains('>')) return true;

            return false;
        }
    }
}