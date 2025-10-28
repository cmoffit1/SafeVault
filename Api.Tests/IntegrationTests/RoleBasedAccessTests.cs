using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class RoleBasedAccessTests
    {
        private WebApplicationFactory<Program>? _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new WebApplicationFactory<Program>();
        }

        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();
        }

        private async Task<string> AuthenticateAsync(System.Net.Http.HttpClient client, string username, string password)
        {
            var resp = await client.PostAsJsonAsync("/authenticate", new { Username = username, Password = password });
            resp.EnsureSuccessStatusCode();
            var j = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            j.TryGetProperty("token", out var t);
            return t.GetString() ?? string.Empty;
        }

        [Test]
        public async Task Admin_Can_Access_AdminEndpoints()
        {
            var client = _factory!.CreateClient();
            // Seeded admin in test config: username=admin, password=ChangeMe123!
            var token = await AuthenticateAsync(client, "admin", "ChangeMe123!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/admin/status");
            Assert.That(resp.IsSuccessStatusCode, Is.True, "Admin should access /admin/status");

            var usersResp = await client.GetAsync("/admin/users");
            Assert.That(usersResp.IsSuccessStatusCode, Is.True, "Admin should access /admin/users");
        }

        [Test]
        public async Task Manager_Cannot_Access_AdminEndpoints()
        {
            var client = _factory!.CreateClient();

            // Create manager user via admin
            var adminToken = await AuthenticateAsync(client, "admin", "ChangeMe123!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var createReq = new { Username = "mgr1", Password = "ManagerPass2025!", Roles = new[] { "Manager" } };
            var createResp = await client.PostAsJsonAsync("/admin/users", createReq);
            Assert.That(createResp.IsSuccessStatusCode, Is.True, "Admin should be able to create manager user");

            // Authenticate as manager
            var token = await AuthenticateAsync(client, "mgr1", "ManagerPass2025!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync("/admin/status");
            Assert.That(resp.StatusCode == System.Net.HttpStatusCode.Forbidden || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized, Is.True, "Manager should not access admin endpoints");
        }

        [Test]
        public async Task User_Cannot_Access_ManagerEndpoints_But_Manager_Can()
        {
            var client = _factory!.CreateClient();

            // Register a regular user
            var regResp = await client.PostAsJsonAsync("/register", new { Username = "normal1", Password = "UserPass2025!" });
            Assert.That(regResp.IsSuccessStatusCode, Is.True, "Register normal user");
            var userToken = await AuthenticateAsync(client, "normal1", "UserPass2025!");

            // Normal user tries manager endpoint
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
            var mgrResp = await client.GetAsync("/manager/users");
            Assert.That(mgrResp.StatusCode == System.Net.HttpStatusCode.Forbidden || mgrResp.StatusCode == System.Net.HttpStatusCode.Unauthorized, Is.True, "Normal user should not access manager endpoints");

            // Create manager via admin
            var adminToken = await AuthenticateAsync(client, "admin", "ChangeMe123!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            var createReq = new { Username = "mgr2", Password = "ManagerPass2025!", Roles = new[] { "Manager" } };
            var createResp = await client.PostAsJsonAsync("/admin/users", createReq);
            Assert.That(createResp.IsSuccessStatusCode, Is.True, "Admin should create manager2");

            // Authenticate as manager2
            var mgrToken = await AuthenticateAsync(client, "mgr2", "ManagerPass2025!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrToken);
            var okResp = await client.GetAsync("/manager/users");
            Assert.That(okResp.IsSuccessStatusCode, Is.True, "Manager should access /manager/users");
        }
    }
}
