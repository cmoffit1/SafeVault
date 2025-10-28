using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class AuthTokenAndAdminTests
    {
        [Test]
        public async Task AdminUser_CanGetToken_AndAccess_AdminEndpoint()
        {
            // Generate a signing key for test
            var signingKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));

            // Expose token settings to the test host via environment variables so Program.cs will pick them up during startup
            System.Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
            System.Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
            System.Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

            var factory = TestHostFixture.Factory!;

            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();

            // Create user via repository and set Admin role
            var username = "adminuser";
            var password = "AdminPass123!";
            var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();
            var hashed = hasher.Hash(password);
            var added = await repo.AddUserAsync(username, hashed);
            Assert.That(added, Is.True, "Failed to add admin user");
            var setRolesOk = await repo.SetRolesAsync(username, new[] { "Admin" });
            Assert.That(setRolesOk, Is.True, "Failed to set roles for admin user");

            var client = factory.CreateClient();

            // Authenticate to get token
            var authResp = await client.PostAsJsonAsync("/authenticate", new { Username = username, Password = password });
            Assert.That(authResp.IsSuccessStatusCode, Is.True, "Authenticate should return success");

            var content = await authResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string>>();
            Assert.That(content, Is.Not.Null);
            Assert.That(content!.ContainsKey("token"), Is.True, "Response should contain token");
            var token = content["token"];

            // Use token to call admin endpoint
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var adminResp = await client.GetAsync("/admin/status");
            if (!adminResp.IsSuccessStatusCode)
            {
                var body = await adminResp.Content.ReadAsStringAsync();
                Assert.Fail($"Admin status failed. Status={adminResp.StatusCode} Body={body}");
            }
        }
    }
}
