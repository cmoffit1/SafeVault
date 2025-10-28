using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class LoginUserManagerRehashTests
    {
        [Test]
        public async Task RehashOnLogin_PersistsNewHash()
        {
            // Configure factory and use the default services
            await using var factory = new WebApplicationFactory<Program>();
            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
            var hasherOld = new Api.Utilities.PasswordHasher(iterations:1, memoryKb:8192, parallelism:1);
            var hasherNew = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>() as Api.Utilities.PasswordHasher;

            var username = "rehashuser";
            var password = "RehashPass!";

            var initialHash = hasherOld.Hash(password);
            var added = await repo.AddUserAsync(username, initialHash);
            Assert.That(added, Is.True, "Failed to add user with old hash");

            // Authenticate via LoginUserManager (from services) to trigger rehash on login
            var manager = scope.ServiceProvider.GetRequiredService<Api.Services.LoginUserManager>();
            var ok = await manager.AuthenticateAsync(username, password);
            Assert.That(ok, Is.True, "Authentication with initial hash failed");

            // Fetch stored hash and ensure it's upgraded (needsUpgrade false for current hasher)
            var stored = await repo.GetHashedPasswordAsync(username);
            Assert.That(stored, Is.Not.Null);
            Assert.That(hasherNew!.NeedsUpgrade(stored!), Is.False);
        }
    }
}
