using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class AdminEndpointsTests
    {
        [Test]
        public async Task AdminUser_CanList_AllUsers()
        {
            var signingKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));
            System.Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
            System.Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
            System.Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

            var factory = TestHostFixture.Factory!;
            using var scope = factory!.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();

            var adminUser = "listadmin";
            var adminPass = "AdminList123!";
            var hashed = hasher.Hash(adminPass);
            var added = await repo.AddUserAsync(adminUser, hashed);
            Assert.That(added, Is.True, "Failed to add admin user");
            var okRoles = await repo.SetRolesAsync(adminUser, new[] { "Admin" });
            Assert.That(okRoles, Is.True, "Failed to set roles for admin");

            var client = factory.CreateClient();
            var authResp = await client.PostAsJsonAsync("/authenticate", new { Username = adminUser, Password = adminPass });
            Assert.That(authResp.IsSuccessStatusCode, Is.True, "Authenticate should return success");
            var content = await authResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string>>();
            Assert.That(content, Is.Not.Null);
            Assert.That(content!.ContainsKey("token"), Is.True);
            var token = content["token"];

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var usersResp = await client.GetAsync("/admin/users");
            if (!usersResp.IsSuccessStatusCode)
            {
                var body = await usersResp.Content.ReadAsStringAsync();
                Assert.Fail($"Admin users listing failed. Status={usersResp.StatusCode} Body={body}");
            }
            var users = await usersResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string[]>>();
            Assert.That(users, Is.Not.Null);
            Assert.That(users!.ContainsKey(adminUser), Is.True, "Listed users should include the admin user");
        }
    }
}
