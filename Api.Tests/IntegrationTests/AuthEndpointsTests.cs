using System.Net.Http.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.IntegrationTests
{
    [TestFixture]
    public class AuthEndpointsTests
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
        public async Task Register_And_Authenticate_EndToEnd()
        {
            var client = _factory!.CreateClient();

            var register = new { Username = "integuser", Password = "StrongPass123!" };
            var regResp = await client.PostAsJsonAsync("/register", register);
            Assert.That(regResp.IsSuccessStatusCode, Is.True, "Register should return success");
            var auth = new { Username = "integuser", Password = "StrongPass123!" };
            var authResp = await client.PostAsJsonAsync("/authenticate", auth);
            Assert.That(authResp.IsSuccessStatusCode, Is.True, "Authenticate should return success");
        }
    }
}
