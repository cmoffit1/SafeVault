using NUnit.Framework;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Api.Repositories;
using Api.Utilities;

namespace Api.Tests
{
    [TestFixture]
    public class TestLoginUserManager
    {
        // Minimal in-memory user store to allow creating a UserManager for tests.
        private class TestUserStore : Microsoft.AspNetCore.Identity.IUserStore<Api.Identity.ApplicationUser>,
            Microsoft.AspNetCore.Identity.IUserPasswordStore<Api.Identity.ApplicationUser>,
            Microsoft.AspNetCore.Identity.IUserRoleStore<Api.Identity.ApplicationUser>
        {
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Api.Identity.ApplicationUser> _users = new();
            private readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.List<string>> _roles = new();

            public System.Threading.Tasks.Task<Microsoft.AspNetCore.Identity.IdentityResult> CreateAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
            {
                user.Id ??= System.Guid.NewGuid().ToString();
                _users[user.UserName ?? string.Empty] = user;
                _roles[user.UserName ?? string.Empty] = new System.Collections.Generic.List<string>();
                return System.Threading.Tasks.Task.FromResult(Microsoft.AspNetCore.Identity.IdentityResult.Success);
            }

            public System.Threading.Tasks.Task<Microsoft.AspNetCore.Identity.IdentityResult> DeleteAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
            {
                _users.TryRemove(user.UserName ?? string.Empty, out _);
                _roles.TryRemove(user.UserName ?? string.Empty, out _);
                return System.Threading.Tasks.Task.FromResult(Microsoft.AspNetCore.Identity.IdentityResult.Success);
            }

            public void Dispose() { }

            public System.Threading.Tasks.Task<Api.Identity.ApplicationUser?> FindByIdAsync(string userId, System.Threading.CancellationToken cancellationToken)
            {
                foreach (var u in _users.Values)
                {
                    if (u.Id == userId) return System.Threading.Tasks.Task.FromResult<Api.Identity.ApplicationUser?>(u);
                }
                return System.Threading.Tasks.Task.FromResult<Api.Identity.ApplicationUser?>(null);
            }

            public System.Threading.Tasks.Task<Api.Identity.ApplicationUser?> FindByNameAsync(string normalizedUserName, System.Threading.CancellationToken cancellationToken)
            {
                foreach (var u in _users.Values)
                {
                    if ((u.NormalizedUserName ?? string.Empty) == normalizedUserName) return System.Threading.Tasks.Task.FromResult<Api.Identity.ApplicationUser?>(u);
                }
                return System.Threading.Tasks.Task.FromResult<Api.Identity.ApplicationUser?>(null);
            }

            public System.Threading.Tasks.Task<string?> GetNormalizedUserNameAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
                => System.Threading.Tasks.Task.FromResult(user.NormalizedUserName);

            public System.Threading.Tasks.Task<string> GetUserIdAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
                => System.Threading.Tasks.Task.FromResult(user.Id ?? string.Empty);

            public System.Threading.Tasks.Task<string?> GetUserNameAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
                => System.Threading.Tasks.Task.FromResult(user.UserName);

            public System.Threading.Tasks.Task SetNormalizedUserNameAsync(Api.Identity.ApplicationUser user, string? normalizedName, System.Threading.CancellationToken cancellationToken)
            {
                user.NormalizedUserName = normalizedName;
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public System.Threading.Tasks.Task SetUserNameAsync(Api.Identity.ApplicationUser user, string? userName, System.Threading.CancellationToken cancellationToken)
            {
                user.UserName = userName;
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public System.Threading.Tasks.Task<Microsoft.AspNetCore.Identity.IdentityResult> UpdateAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
            {
                _users[user.UserName ?? string.Empty] = user;
                return System.Threading.Tasks.Task.FromResult(Microsoft.AspNetCore.Identity.IdentityResult.Success);
            }

            // Password store
            public System.Threading.Tasks.Task SetPasswordHashAsync(Api.Identity.ApplicationUser user, string? passwordHash, System.Threading.CancellationToken cancellationToken)
            {
                user.PasswordHash = passwordHash;
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public System.Threading.Tasks.Task<string?> GetPasswordHashAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
                => System.Threading.Tasks.Task.FromResult(user.PasswordHash);

            public System.Threading.Tasks.Task<bool> HasPasswordAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
                => System.Threading.Tasks.Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));

            // Roles
            public System.Threading.Tasks.Task AddToRoleAsync(Api.Identity.ApplicationUser user, string roleName, System.Threading.CancellationToken cancellationToken)
            {
                var list = _roles.GetOrAdd(user.UserName ?? string.Empty, _ => new System.Collections.Generic.List<string>());
                if (!list.Contains(roleName)) list.Add(roleName);
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public System.Threading.Tasks.Task RemoveFromRoleAsync(Api.Identity.ApplicationUser user, string roleName, System.Threading.CancellationToken cancellationToken)
            {
                if (_roles.TryGetValue(user.UserName ?? string.Empty, out var list)) list.Remove(roleName);
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public System.Threading.Tasks.Task<System.Collections.Generic.IList<string>> GetRolesAsync(Api.Identity.ApplicationUser user, System.Threading.CancellationToken cancellationToken)
            {
                if (_roles.TryGetValue(user.UserName ?? string.Empty, out var list))
                    return System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IList<string>>(list);
                return System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IList<string>>(new System.Collections.Generic.List<string>());
            }

            public System.Threading.Tasks.Task<bool> IsInRoleAsync(Api.Identity.ApplicationUser user, string roleName, System.Threading.CancellationToken cancellationToken)
            {
                if (_roles.TryGetValue(user.UserName ?? string.Empty, out var list))
                    return System.Threading.Tasks.Task.FromResult(list.Contains(roleName));
                return System.Threading.Tasks.Task.FromResult(false);
            }

            public System.Threading.Tasks.Task<System.Collections.Generic.IList<Api.Identity.ApplicationUser>> GetUsersInRoleAsync(string roleName, System.Threading.CancellationToken cancellationToken)
            {
                var ret = new System.Collections.Generic.List<Api.Identity.ApplicationUser>();
                foreach (var kv in _roles)
                {
                    if (kv.Value.Contains(roleName) && _users.TryGetValue(kv.Key, out var u)) ret.Add(u);
                }
                return System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IList<Api.Identity.ApplicationUser>>(ret);
            }
        }

        private LoginUserManager CreateLoginUserManager()
        {
            var store = new TestUserStore();
            var options = Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Identity.IdentityOptions());
            var userValidators = new System.Collections.Generic.List<Microsoft.AspNetCore.Identity.IUserValidator<Api.Identity.ApplicationUser>>();
            var pwdValidators = new System.Collections.Generic.List<Microsoft.AspNetCore.Identity.IPasswordValidator<Api.Identity.ApplicationUser>>();
            var lookupNormalizer = new Microsoft.AspNetCore.Identity.UpperInvariantLookupNormalizer();
            var um = new Microsoft.AspNetCore.Identity.UserManager<Api.Identity.ApplicationUser>(
                store,
                options,
                new Microsoft.AspNetCore.Identity.PasswordHasher<Api.Identity.ApplicationUser>(),
                userValidators,
                pwdValidators,
                lookupNormalizer,
                new Microsoft.AspNetCore.Identity.IdentityErrorDescriber(),
                null,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<Microsoft.AspNetCore.Identity.UserManager<Api.Identity.ApplicationUser>>.Instance
            );

            // For unit tests, allow a permissive password policy so short test passwords
            // used in these unit tests succeed. Integration tests exercise the real
            // policy via the Program configuration.
            // Use a small but non-trivial minimum so extremely short passwords (length 1)
            // still fail while typical test passwords (>=8) succeed.
            var policy = new Api.Utilities.PasswordPolicy { MinLength = 2 };
            return new LoginUserManager(um, policy: policy);
        }
        [Test]
        public async Task Register_ValidUser_Succeeds()
        {
            var manager = CreateLoginUserManager();
            var user = new LoginUser { Username = "testuser", Password = "P@ssw0rd" };

            var result = await manager.RegisterAsync(user);

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task Register_DuplicateUser_Fails()
        {
            var manager = CreateLoginUserManager();
            var user = new LoginUser { Username = "dupuser", Password = "secret12" };
            Assert.That(await manager.RegisterAsync(user), Is.True);

            var second = new LoginUser { Username = "dupuser", Password = "other" };
            Assert.That(await manager.RegisterAsync(second), Is.False);
        }

        [Test]
        public async Task Register_InvalidInput_Fails()
        {
            var manager = CreateLoginUserManager();
            var user = new LoginUser { Username = "inva!id<>", Password = "" };

            Assert.That(await manager.RegisterAsync(user), Is.False);
        }

        [Test]
        public async Task Authenticate_ValidCredentials_Succeeds()
        {
            var manager = CreateLoginUserManager();
            var user = new LoginUser { Username = "authuser", Password = "pass1234" };
            Assert.That(await manager.RegisterAsync(user), Is.True);

            var ok = await manager.AuthenticateAsync("authuser", "pass1234");
            Assert.That(ok, Is.True);
        }

        [Test]
        public async Task Authenticate_WrongPassword_Fails()
        {
            var manager = CreateLoginUserManager();
            var user = new LoginUser { Username = "authuser2", Password = "correct12" };
            Assert.That(await manager.RegisterAsync(user), Is.True);

            var ok = await manager.AuthenticateAsync("authuser2", "wrongpass");
            Assert.That(ok, Is.False);
        }

        [Test]
        public async Task Authenticate_SanitizationApplied()
        {
            var manager = CreateLoginUserManager();
            // Too-short password should fail registration
            var user = new LoginUser { Username = "Robert'); DROP TABLE Users;--", Password = "p" };
            Assert.That(await manager.RegisterAsync(user), Is.False);

            // If we register a sanitized/valid user it should succeed and authenticate.
            var valid = new LoginUser { Username = "Robert'); DROP TABLE Users;--", Password = "strongPass1" };
            Assert.That(await manager.RegisterAsync(valid), Is.True);

            var ok = await manager.AuthenticateAsync("Robert'); DROP TABLE Users;--", "strongPass1");
            Assert.That(ok, Is.True);

            var ok2 = await manager.AuthenticateAsync("Robert DROP TABLE Users", "strongPass1");
            Assert.That(ok2, Is.True);
        }
    }
}
