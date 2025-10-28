using System.Text.RegularExpressions;

namespace Api.Utilities
{
    public static class HtmlSanitizer
    {
        public static string SanitizeForHtml(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var s = input;
            s = s.Replace("\0", string.Empty);

            // Remove script tags and their content
            s = Regex.Replace(s, @"<script.*?>.*?</script>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Remove any on* attributes (onclick, onerror, ...)
                s = Regex.Replace(s, @"on[A-Za-z]+\b\s*=\s*""[^""]*""", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"on[A-Za-z]+\b\s*=\s*'[^']*'", string.Empty, RegexOptions.IgnoreCase);

            // Remove javascript: URIs and data:text/html URIs
            s = Regex.Replace(s, @"javascript:\s*", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"data:\s*text/html[^\s>]*", string.Empty, RegexOptions.IgnoreCase);

            // Strip any remaining angle brackets and encode for safe rendering
            s = s.Replace("<", string.Empty).Replace(">", string.Empty);
            s = System.Net.WebUtility.HtmlEncode(s);
            return s;
        }

        public static bool IsLikelyXssAttempt(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input;

            if (Regex.IsMatch(s, @"(?i)<script\b")) return true;
            if (Regex.IsMatch(s, @"(?i)on[a-z]+\s*=")) return true;
            if (Regex.IsMatch(s, @"(?i)javascript:\s*")) return true;
            if (Regex.IsMatch(s, @"data:\s*text/html", RegexOptions.IgnoreCase)) return true;
            if (s.Contains('<') || s.Contains('>')) return true;

            return false;
        }
    }
}
