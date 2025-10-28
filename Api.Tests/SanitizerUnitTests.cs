using NUnit.Framework;

namespace Api.Tests
{
    [TestFixture]
    public class SanitizerUnitTests
    {
        [Test]
        public void HtmlSanitizer_RemovesScriptTagsAndEventHandlers_AndEncodes()
        {
            var raw = "<script>alert('x')</script><div onclick=\"doIt()\">Hello <b>World</b></div>";
            var sanitized = Api.Utilities.HtmlSanitizer.SanitizeForHtml(raw);

            // Should not contain script tag or onclick attribute
            Assert.That(sanitized.ToLowerInvariant().Contains("script"), Is.False, "sanitized should not contain script");
            Assert.That(sanitized.ToLowerInvariant().Contains("onclick"), Is.False, "sanitized should not contain onclick");

            // Should contain plain text 'Hello World' (HTML encoded)
            Assert.That(sanitized.Contains("Hello") && sanitized.Contains("World"), Is.True, "sanitized should preserve visible text");

            // Should not contain angle brackets
            Assert.That(sanitized.Contains("<") || sanitized.Contains(">"), Is.False, "sanitized should strip angle brackets");
        }

        [Test]
        public void ValidationHelpers_Sanitize_CollapsesWhitespaceAndRemovesControls()
        {
            var raw = "  This\tis\n\r\n a   test\u0000";
            var s = Api.Utilities.ValidationHelpers.Sanitize(raw);

            Assert.That(s.Contains('\t') || s.Contains('\n') || s.Contains('\r') || s.Contains('\0'), Is.False);
            // Note: control characters like tab are removed (not replaced with spaces).
            Assert.That(s, Is.EqualTo("Thisis a test"));
        }

        [Test]
        public void UsernameSanitizer_RemovesIllegalCharacters_AndLowercases()
        {
            var raw = "User! Name-Example@Domain.COM";
            var u = Api.Utilities.UsernameSanitizer.SanitizeUsername(raw);

            // Should remove '!' and space, remove hyphen (not allowed per sanitizer) and lowercase
            Assert.That(u, Is.EqualTo("usernameexample@domain.com"));
        }

        [Test]
        public void InputValidation_Sanitize_NoKeywordStripping_PreservesWords()
        {
            var raw = "select or drop table";
            var s = Api.Utilities.InputValidation.Sanitize(raw);

            // Should keep the words (we no longer strip SQL keywords)
            Assert.That(s.Contains("select") && s.Contains("drop") && s.Contains("table"), Is.True);
        }
    }
}
