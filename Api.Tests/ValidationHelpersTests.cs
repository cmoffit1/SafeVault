using NUnit.Framework;
using Api.Utilities;

namespace Api.Tests
{
    public class ValidationHelpersTests
    {
        [Test]
        public void SanitizeForHtml_RemovesScriptTagsAndEventHandlers()
        {
            var input = "<div onclick=\"alert(1)\">Click</div><script>alert('xss')</script>hello";
            var sanitized = HtmlSanitizer.SanitizeForHtml(input);
            Assert.That(sanitized.Contains("<script"), Is.False, "Should remove script tags");
            Assert.That(sanitized.Contains("onclick"), Is.False, "Should remove event handler attributes");
            Assert.That(sanitized.Contains("hello"), Is.True, "Should preserve safe text");
        }

        [Test]
        public void SanitizeForHtml_EncodesHtmlEntities()
        {
            var input = "<b>bold</b> & <i>italic</i>";
            var sanitized = HtmlSanitizer.SanitizeForHtml(input);
            // angle brackets removed and remaining text is encoded
            Assert.That(sanitized.Contains('<'), Is.False);
            Assert.That(sanitized.Contains('>'), Is.False);
            Assert.That(sanitized.Contains("bold"), Is.True);
        }

        [Test]
        public void IsLikelyXssAttempt_DetectsCommonPatterns()
        {
            Assert.That(HtmlSanitizer.IsLikelyXssAttempt("<script>alert(1)</script>"), Is.True);
            Assert.That(HtmlSanitizer.IsLikelyXssAttempt("<img src=\"x\" onerror=\"alert(1)\">"), Is.True);
            Assert.That(HtmlSanitizer.IsLikelyXssAttempt("javascript:alert(1)"), Is.True);
            Assert.That(HtmlSanitizer.IsLikelyXssAttempt("Hello world"), Is.False);
        }
    }
}
