using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class RegisterLoginIntegrationTests
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

        [Test]
        public async Task Register_Then_Authenticate_ReturnsToken_And_Allows_Authorized_Call()
        {
            var client = _factory!.CreateClient();

            var username = "integ_register_user";
            var password = "StrongPass123!";

            // Register
            var register = new { Username = username, Password = password };
            var regResp = await client.PostAsJsonAsync("/register", register);
            Assert.That(regResp.IsSuccessStatusCode, Is.True, "Register should return success");

            // Authenticate and get token
            var auth = new { Username = username, Password = password };
            var authResp = await client.PostAsJsonAsync("/authenticate", auth);
            Assert.That(authResp.IsSuccessStatusCode, Is.True, "Authenticate should return success");

            var j = await authResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.That(j.TryGetProperty("token", out var tokElem), Is.True, "Response should contain token property");
            var token = tokElem.GetString();
            Assert.That(string.IsNullOrEmpty(token), Is.False, "Token should be non-empty");

            // Use token to call an authenticated endpoint
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var tasksResp = await client.GetAsync("/tasks");
            Assert.That(tasksResp.IsSuccessStatusCode, Is.True, "Authenticated request to /tasks should succeed");
        }

        [Test]
        public async Task Unauthenticated_Call_To_Protected_Endpoint_Returns_401()
        {
            var client = _factory!.CreateClient();
            var resp = await client.GetAsync("/tasks");
            Assert.That(resp.StatusCode == System.Net.HttpStatusCode.Unauthorized || resp.StatusCode == System.Net.HttpStatusCode.Redirect, Is.True, "Unauthenticated /tasks should be unauthorized or redirect in some hosts");
        }
    }
}
