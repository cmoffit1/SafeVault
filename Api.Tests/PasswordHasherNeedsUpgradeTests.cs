using NUnit.Framework;
using Api.Utilities;

namespace Api.Tests
{
    public class PasswordHasherNeedsUpgradeTests
    {
        [Test]
        public void NeedsUpgrade_ReturnsTrueWhenParamsLower()
        {
            var current = new PasswordHasher(iterations:3, memoryKb:65536, parallelism:1);
            var older = new PasswordHasher(iterations:2, memoryKb:32768, parallelism:1);

            var pwd = "AnotherPass!";
            var oldHash = older.Hash(pwd);

            Assert.That(current.NeedsUpgrade(oldHash), Is.True);
            Assert.That(older.NeedsUpgrade(oldHash), Is.False);
        }
    }
}
