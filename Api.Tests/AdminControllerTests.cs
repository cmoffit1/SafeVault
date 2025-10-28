using NUnit.Framework;
using Api.Controllers;
using Api.Dtos;
using Api.Repositories;
using Api.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests
{
    public class AdminControllerTests
    {
        [Test]
        public async System.Threading.Tasks.Task CreateUser_AdminCanCreateUser()
        {
            var repo = new InMemoryUserRepository();
            var hasher = new PasswordHasher(iterations:1, memoryKb:8192, parallelism:1);
            var policy = new PasswordPolicy();

            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AdminController>.Instance;
            var controller = new AdminController(logger);

            var req = new AdminCreateUserRequest
            {
                Username = "newuser",
                Password = "TestPass123!",
                Roles = new[] { "User" }
            };

            var result = await controller.CreateUser(req, repo, hasher, policy);

            // Controller now returns a ContentResult with JSON payload and status code 201
            Assert.That(result, Is.InstanceOf<Microsoft.AspNetCore.Mvc.ContentResult>());
            var content = (Microsoft.AspNetCore.Mvc.ContentResult)result;
            Assert.That(content.StatusCode, Is.EqualTo(201));

            var hashed = await repo.GetHashedPasswordAsync("newuser");
            Assert.That(hashed, Is.Not.Null.And.Not.Empty);
            Assert.That(hasher.Verify(req.Password, hashed), Is.True);

            var roles = await repo.GetRolesAsync("newuser");
            Assert.That(roles, Is.Not.Null);
            Assert.That(roles, Does.Contain("User"));
        }
    }
}
