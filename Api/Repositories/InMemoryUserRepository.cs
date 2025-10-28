using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Api.Repositories
{
    public class InMemoryUserRepository : IUserRepository
    {
        // Simulate normalized storage: users and separate user->roles mapping
        private readonly ConcurrentDictionary<string, string> _users = new(); // username -> hashedPassword
        private readonly ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>> _userRoles = new(); // username -> set of role names
    // username -> managerUsername (both keys are stored lower-case). managerUsername may be null or empty for no manager.
    private readonly ConcurrentDictionary<string, string?> _managerOfUser = new();
        private readonly Microsoft.Extensions.Logging.ILogger<InMemoryUserRepository> _logger;

        public InMemoryUserRepository(Microsoft.Extensions.Logging.ILogger<InMemoryUserRepository>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryUserRepository>.Instance;
        }

        public Task<bool> AddUserAsync(string username, string hashedPassword)
            => AddUserAsync(username, hashedPassword, System.Array.Empty<string>());

        public Task<bool> AddUserAsync(string username, string hashedPassword, string[] roles)
        {
            var key = username.ToLowerInvariant();
            var added = _users.TryAdd(key, hashedPassword);
            if (added)
            {
                var roleMap = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(System.StringComparer.OrdinalIgnoreCase);
                if (roles != null)
                {
                    foreach (var r in roles)
                    {
                        if (string.IsNullOrWhiteSpace(r)) continue;
                        roleMap[r] = 0;
                    }
                }
                _userRoles[key] = roleMap;
                // initialize manager mapping entry
                _managerOfUser[key] = null;
                _logger.LogDebug("InMemoryUserRepository: added user '{Username}'", key);
            }
            else
            {
                _logger.LogDebug("InMemoryUserRepository: failed to add user '{Username}' (exists)", key);
            }
            return Task.FromResult(added);
        }

        public Task<string?> GetHashedPasswordAsync(string username)
        {
            var key = username.ToLowerInvariant();
            _users.TryGetValue(key, out var hashed);
            _logger.LogDebug("InMemoryUserRepository: lookup for user '{Username}' {Found}", key, hashed != null);
            return Task.FromResult(hashed);
        }

        public Task<string[]?> GetRolesAsync(string username)
        {
            var key = username.ToLowerInvariant();
            if (_userRoles.TryGetValue(key, out var map))
            {
                var roles = map.Keys.ToArray();
                return Task.FromResult<string[]?>(roles);
            }
            return Task.FromResult<string[]?>(null);
        }

        public Task<bool> SetRolesAsync(string username, string[] roles)
        {
            var key = username.ToLowerInvariant();
            if (!_users.ContainsKey(key)) return Task.FromResult(false);
            var roleMap = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(System.StringComparer.OrdinalIgnoreCase);
            if (roles != null)
            {
                foreach (var r in roles)
                {
                    if (string.IsNullOrWhiteSpace(r)) continue;
                    roleMap[r] = 0;
                }
            }
            _userRoles[key] = roleMap;
            // ensure manager mapping exists for user
            _managerOfUser.TryAdd(key, null);
            _logger.LogDebug("InMemoryUserRepository: set roles for user '{Username}'", key);
            return Task.FromResult(true);
        }

        public Task<bool> UpdatePasswordAsync(string username, string newHashedPassword)
        {
            var key = username.ToLowerInvariant();
            if (!_users.ContainsKey(key)) return Task.FromResult(false);
            _users[key] = newHashedPassword;
            _logger.LogDebug("InMemoryUserRepository: updated password for user '{Username}'", key);
            return Task.FromResult(true);
        }

        public Task<System.Collections.Generic.Dictionary<string, string[]>> GetAllUsersAsync()
        {
            var dict = new System.Collections.Generic.Dictionary<string, string[]>();
            foreach (var kv in _users)
            {
                var roles = System.Array.Empty<string>();
                if (_userRoles.TryGetValue(kv.Key, out var map)) roles = map.Keys.ToArray();
                dict[kv.Key] = roles;
            }
            return Task.FromResult(dict);
        }

        public Task<bool> SetManagerAsync(string username, string managerUsername)
        {
            var key = username.ToLowerInvariant();
            if (!_users.ContainsKey(key)) return Task.FromResult(false);

            if (string.IsNullOrWhiteSpace(managerUsername))
            {
                // clear manager
                _managerOfUser[key] = null;
                return Task.FromResult(true);
            }

            var mgrKey = managerUsername.ToLowerInvariant();
            if (!_users.ContainsKey(mgrKey)) return Task.FromResult(false);

            _managerOfUser[key] = mgrKey;
            _logger.LogDebug("InMemoryUserRepository: set manager for user '{Username}' to '{Manager}'", key, mgrKey);
            return Task.FromResult(true);
        }

        public Task<string[]?> GetUsersForManagerAsync(string managerUsername)
        {
            var mgrKey = managerUsername.ToLowerInvariant();
            if (!_users.ContainsKey(mgrKey)) return Task.FromResult<string[]?>(null);
            var list = new System.Collections.Generic.List<string>();
            foreach (var kv in _managerOfUser)
            {
                if (kv.Value != null && string.Equals(kv.Value, mgrKey, System.StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(kv.Key);
                }
            }
            return Task.FromResult<string[]?>(list.ToArray());
        }

        public Task<string[]> GetAllRolesAsync()
        {
            var set = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _userRoles)
            {
                foreach (var r in kv.Value.Keys)
                {
                    if (!string.IsNullOrWhiteSpace(r)) set.Add(r);
                }
            }
            var arr = new System.Collections.Generic.List<string>(set);
            arr.Sort(StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(arr.ToArray());
        }
    }
}
