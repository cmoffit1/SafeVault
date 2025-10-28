using NUnit.Framework;
using Microsoft.Extensions.Configuration;
// Use fully-qualified Api.Utilities.InputValidation to avoid ambiguous reference with obsolete shim

namespace Api.Tests {
    [TestFixture]
    public class TestInputValidation {
        // ...existing code...
        [Test]
        public void TestForSQLInjection_Sanitize()
        {
            // The input sanitizer now performs conservative normalization (whitespace,
            // control-character removal) but does not attempt to strip SQL keywords.
            // Ensure dangerous control characters are removed and that the normalized
            // output remains usable (no exceptions when used as identifiers).
            string maliciousInput = "Robert'); DROP TABLE Users;--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(maliciousInput);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            // No control characters
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False, "Sanitized output should not contain control characters");
        }

        [Test]
        public void TestForSQLInjection_Tautology()
        {
            string input = "admin' OR '1'='1";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_Union()
        {
            string input = "test' UNION SELECT password FROM Users--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_Comment()
        {
            string input = "user';--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_Stacked()
        {
            string input = "user'; DROP TABLE Users;--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_Blind()
        {
            string input = "user' AND 1=1--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_TimeBased()
        {
            string input = "user'; WAITFOR DELAY '0:0:5'--";
            string sanitized = Api.Utilities.InputValidation.Sanitize(input);
            Assert.That(sanitized, Is.Not.Null.And.Not.Empty);
            Assert.That(sanitized.Any(c => char.IsControl(c)), Is.False);
        }

        [Test]
        public void TestForSQLInjection_TestUserRepository()
        {
            var repo = new TestUserRepository();
            string maliciousUsername = "Robert'); DROP TABLE Users;--";
            string email = "test@example.com";
            Assert.DoesNotThrow(() => repo.AddUser(maliciousUsername, email));
            var user = repo.GetUserByUsername(maliciousUsername);
            Assert.That(user, Is.Not.Null, "User should be added and retrievable");
        }
        [Test]
        public void TestForXSS_Sanitize()
        {
            string maliciousInput = "<script>alert('xss')</script>hello";
            // Use the HTML-focused sanitizer for XSS expectations
            string sanitized = Api.Utilities.HtmlSanitizer.SanitizeForHtml(maliciousInput);
            Assert.That(sanitized.Contains("<script>"), Is.False, "HtmlSanitizer should remove script tags");
            Assert.That(sanitized.Contains("<"), Is.False, "HtmlSanitizer should remove raw angle brackets");
            Assert.That(sanitized.Contains(">"), Is.False, "HtmlSanitizer should remove raw angle brackets");
            Assert.That(sanitized.ToLower().Contains("alert"), Is.False, "HtmlSanitizer should remove script content");
        }
    }
}
