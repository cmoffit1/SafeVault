using NUnit.Framework;
using Api.Utilities;

namespace Api.Tests
{
    public class PasswordHasherTests
    {
        [Test]
        public void HashAndVerify_Succeeds()
        {
            var hasher = new PasswordHasher(iterations:2, memoryKb:32768, parallelism:1);
            var pwd = "TestPass123!";
            var h = hasher.Hash(pwd);
            Assert.That(h, Is.Not.Null);
            Assert.That(hasher.Verify(pwd, h), Is.True);
            Assert.That(hasher.Verify(pwd + "x", h), Is.False);
        }
    }
}
