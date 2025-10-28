using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class ManagerEndpointsAuthorizationTests
    {
        [Test]
        public async Task Unauthenticated_CannotAccess_ManagerEndpoints_Returns_Unauthorized()
        {
            var signingKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));
            System.Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
            System.Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
            System.Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

            var factory = TestHostFixture.Factory!;
            var client = factory.CreateClient();

            // Unauthenticated GET should return 401 Unauthorized
            var getResp = await client.GetAsync("/admin/users/someuser/manager");
            Assert.That(getResp.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));

            // Unauthenticated POST should return 401 Unauthorized
            var postResp = await client.PostAsJsonAsync("/admin/users/someuser/manager", new { manager = "manager1" });
            Assert.That(postResp.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task NonAdminAuthenticated_CannotAssign_Manager_Returns_Forbidden()
        {
            var signingKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));
            System.Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
            System.Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
            System.Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

            var factory = TestHostFixture.Factory!;
            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();

            // Create a non-admin user
            var normalUser = "normal_user";
            var normalPass = "Normal123!";
            var normalHashed = hasher.Hash(normalPass);
            Assert.That(await repo.AddUserAsync(normalUser, normalHashed), Is.True);
            Assert.That(await repo.SetRolesAsync(normalUser, new[] { "User" }), Is.True);

            var client = factory.CreateClient();

            // Authenticate as the normal user
            var authResp = await client.PostAsJsonAsync("/authenticate", new { Username = normalUser, Password = normalPass });
            Assert.That(authResp.IsSuccessStatusCode, Is.True);
            var authContent = await authResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string>>();
            Assert.That(authContent, Is.Not.Null);
            var token = authContent!["token"];
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Non-admin should get 403 Forbidden when attempting to assign a manager
            var postResp = await client.PostAsJsonAsync($"/admin/users/{normalUser}/manager", new { manager = "manager1" });
            Assert.That(postResp.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Forbidden));

            // And a GET should also be forbidden
            var getResp = await client.GetAsync($"/admin/users/{normalUser}/manager");
            Assert.That(getResp.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Forbidden));
        }
    }
}
