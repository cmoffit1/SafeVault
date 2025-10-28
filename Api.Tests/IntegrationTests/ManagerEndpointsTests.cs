using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class ManagerEndpointsTests
    {
        [Test]
        public async Task Admin_CanAssign_AndClear_ManagerForUser()
        {
            var signingKey = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));
            System.Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
            System.Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
            System.Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

            var factory = TestHostFixture.Factory!;
            using var scope = factory.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
            var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();

            // Create an admin user to authenticate as
            var adminUser = "mgr_admin";
            var adminPass = "AdminMgr123!";
            var adminHashed = hasher.Hash(adminPass);
            Assert.That(await repo.AddUserAsync(adminUser, adminHashed), Is.True);
            Assert.That(await repo.SetRolesAsync(adminUser, new[] { "Admin" }), Is.True);

            // Create a manager candidate
            var managerUser = "manager1";
            var managerPass = "ManagerOne123!";
            var managerHashed = hasher.Hash(managerPass);
            Assert.That(await repo.AddUserAsync(managerUser, managerHashed), Is.True);
            Assert.That(await repo.SetRolesAsync(managerUser, new[] { "Manager" }), Is.True);

            // Create a normal target user
            var targetUser = "targetuser";
            var targetPass = "Target123!";
            var targetHashed = hasher.Hash(targetPass);
            Assert.That(await repo.AddUserAsync(targetUser, targetHashed), Is.True);
            Assert.That(await repo.SetRolesAsync(targetUser, new[] { "User" }), Is.True);

            var client = factory.CreateClient();

            // Authenticate as admin
            var authResp = await client.PostAsJsonAsync("/authenticate", new { Username = adminUser, Password = adminPass });
            Assert.That(authResp.IsSuccessStatusCode, Is.True, "Authenticate should succeed for admin");
            var authContent = await authResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string>>();
            Assert.That(authContent, Is.Not.Null);
            Assert.That(authContent!.ContainsKey("token"), Is.True);
            var token = authContent["token"];

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Initially, manager should be null
            var getResp = await client.GetAsync($"/admin/users/{targetUser}/manager");
            Assert.That(getResp.IsSuccessStatusCode, Is.True, "GET manager should succeed");
            var getBody = await getResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string?>>();
            Assert.That(getBody, Is.Not.Null);
            Assert.That(getBody!.ContainsKey("manager"));
            Assert.That(getBody["manager"], Is.Null, "Manager should initially be null");

            // Assign manager
            var assignResp = await client.PostAsJsonAsync($"/admin/users/{targetUser}/manager", new { manager = managerUser });
            Assert.That(assignResp.IsSuccessStatusCode, Is.True, "Assign manager should succeed");
            var assignBody = await assignResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string?>>();
            Assert.That(assignBody, Is.Not.Null);
            Assert.That(assignBody!["manager"], Is.EqualTo(managerUser));

            // Verify via GET
            var getResp2 = await client.GetAsync($"/admin/users/{targetUser}/manager");
            Assert.That(getResp2.IsSuccessStatusCode, Is.True);
            var getBody2 = await getResp2.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string?>>();
            Assert.That(getBody2, Is.Not.Null);
            Assert.That(getBody2!["manager"], Is.EqualTo(managerUser));

            // Clear manager (send null)
            var clearResp = await client.PostAsJsonAsync($"/admin/users/{targetUser}/manager", new { manager = (string?)null });
            Assert.That(clearResp.IsSuccessStatusCode, Is.True, "Clearing manager should succeed");
            var clearBody = await clearResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string?>>();
            Assert.That(clearBody, Is.Not.Null);
            Assert.That(clearBody!["manager"], Is.Null);

            // Verify cleared
            var getResp3 = await client.GetAsync($"/admin/users/{targetUser}/manager");
            Assert.That(getResp3.IsSuccessStatusCode, Is.True);
            var getBody3 = await getResp3.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string?>>();
            Assert.That(getBody3, Is.Not.Null);
            Assert.That(getBody3!["manager"], Is.Null, "Manager should be null after clearing");
        }
    }
}
