using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Api.Identity;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Api.Repositories
{
    public class IdentityUserRepository : IUserRepository
    {
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<IdentityUserRepository> _logger;

        public IdentityUserRepository(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext db, ILogger<IdentityUserRepository> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _logger = logger;
        }

        public async Task<bool> AddUserAsync(string username, string hashedPassword)
        {
            // Create user without a password and set the password hash directly
            var user = new ApplicationUser { UserName = username, NormalizedUserName = username.ToUpperInvariant() };
            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger?.LogWarning("CreateAsync failed for '{User}': {Errors}", username, string.Join(';', createResult.Errors.Select(e => e.Description)));
                return false;
            }

            if (!string.IsNullOrEmpty(hashedPassword))
            {
                // Set PasswordHash directly (used for migration compatibility)
                user.PasswordHash = hashedPassword;
                var upd = await _userManager.UpdateAsync(user);
                if (!upd.Succeeded)
                {
                    _logger?.LogWarning("UpdateAsync failed for '{User}' when setting PasswordHash: {Errors}", username, string.Join(';', upd.Errors.Select(e => e.Description)));
                    return false;
                }
            }

            return true;
        }

        public async Task<bool> AddUserAsync(string username, string hashedPassword, string[] roles)
        {
            var created = await AddUserAsync(username, hashedPassword);
            if (!created) return false;

            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return false;

            // Ensure roles exist
            foreach (var role in roles ?? System.Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(role)) continue;
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    var roleCreate = await _roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleCreate.Succeeded)
                    {
                        _logger?.LogWarning("Role creation failed for '{Role}' while seeding user '{User}': {Errors}", role, username, string.Join(';', roleCreate.Errors.Select(e => e.Description)));
                        return false;
                    }
                }
            }

            var addRoles = await _userManager.AddToRolesAsync(user, roles ?? System.Array.Empty<string>());
            if (!addRoles.Succeeded)
            {
                _logger?.LogWarning("AddToRolesAsync failed for '{User}': {Errors}", username, string.Join(';', addRoles.Errors.Select(e => e.Description)));
                return false;
            }

            return true;
        }

        public async Task<string?> GetHashedPasswordAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            return user?.PasswordHash;
        }

        public async Task<string[]?> GetRolesAsync(string username)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return null;
            var roles = await _userManager.GetRolesAsync(user);
            return roles.ToArray();
        }

        public async Task<bool> SetRolesAsync(string username, string[] roles)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return false;

            var current = await _userManager.GetRolesAsync(user);
            var remove = current.Except(roles ?? System.Array.Empty<string>());
            var add = (roles ?? System.Array.Empty<string>()).Except(current);

            if (remove.Any())
            {
                var r = await _userManager.RemoveFromRolesAsync(user, remove);
                if (!r.Succeeded) return false;
            }

            if (add.Any())
            {
                foreach (var role in add)
                {
                    if (!await _roleManager.RoleExistsAsync(role))
                        await _roleManager.CreateAsync(new IdentityRole(role));
                }
                var r = await _userManager.AddToRolesAsync(user, add);
                if (!r.Succeeded) return false;
            }

            return true;
        }

        public async Task<bool> UpdatePasswordAsync(string username, string newHashedPassword)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return false;
            user.PasswordHash = newHashedPassword;
            var r = await _userManager.UpdateAsync(user);
            return r.Succeeded;
        }

        public async Task<Dictionary<string, string[]>> GetAllUsersAsync()
        {
            var dict = new Dictionary<string, List<string>>();
            var users = await _userManager.Users.ToListAsync();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (!dict.TryGetValue(u.UserName ?? string.Empty, out var list))
                {
                    list = new List<string>();
                    dict[u.UserName ?? string.Empty] = list;
                }
                list.AddRange(roles);
            }

            var result = new Dictionary<string, string[]>(dict.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict) result[kv.Key] = kv.Value.Count == 0 ? System.Array.Empty<string>() : kv.Value.ToArray();
            return result;
        }

        public async Task<string[]> GetAllRolesAsync()
        {
            var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return roles.Where(r => r != null).Select(r => r!).ToArray();
        }

        public async Task<bool> SetManagerAsync(string username, string managerUsername)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null) return false;
            user.ManagerUsername = managerUsername;
            var r = await _userManager.UpdateAsync(user);
            return r.Succeeded;
        }

        public async Task<string[]?> GetUsersForManagerAsync(string managerUsername)
        {
            if (string.IsNullOrEmpty(managerUsername)) return null;
            var users = await _userManager.Users.Where(u => u.ManagerUsername == managerUsername).Select(u => u.UserName).ToListAsync();
            return users.ToArray();
        }
    }
}
