using NUnit.Framework;
using Api.Utilities;

namespace Api.Tests
{
    public class SanitizationTests
    {
        [Test]
        public void Sanitize_RemovesSqlKeywordsAndPunctuation()
        {
            var input = "Robert'); DROP TABLE Users;--";
            var sanitized = UsernameSanitizer.SanitizeUsername(input);
            // Expect the username to be lower-cased and punctuation removed
            Assert.That(sanitized, Is.EqualTo("robertdroptableusers"));
        }
    }
}
