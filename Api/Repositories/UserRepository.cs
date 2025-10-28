using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace Api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        private readonly Microsoft.Extensions.Logging.ILogger<UserRepository> _logger;

        public UserRepository(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<UserRepository>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UserRepository>.Instance;
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new ArgumentException("Missing connection string 'DefaultConnection' in configuration.");
        }

        public async Task<bool> AddUserAsync(string username, string hashedPassword)
        {
            return await AddUserAsync(username, hashedPassword, System.Array.Empty<string>());
        }

        public async Task<bool> AddUserAsync(string username, string hashedPassword, string[] roles)
        {
            // Insert into Users and populate Roles/UserRoles in a transaction
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            var tx = conn.BeginTransaction();
            try
            {
                // Insert user
                const string insertUser = "INSERT INTO Users (Username, HashedPassword) VALUES (@username, @hashedPassword);";
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = insertUser;
                    cmd.CommandType = CommandType.Text;
                    cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });
                    cmd.Parameters.Add(new SqlParameter("@hashedPassword", SqlDbType.NVarChar, 512) { Value = hashedPassword });
                    var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    if (rows <= 0)
                    {
                        tx.Rollback();
                        return false;
                    }
                }

                // Get user id
                int userId;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT Id FROM Users WHERE Username = @username;";
                    cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });
                    var obj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    userId = Convert.ToInt32(obj!);
                }

                // For each role, ensure role exists and insert mapping
                if (roles != null && roles.Length > 0)
                {
                    foreach (var role in roles)
                    {
                        if (string.IsNullOrWhiteSpace(role)) continue;
                        int roleId;
                        await using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = @roleName)
    INSERT INTO Roles (Name) VALUES (@roleName);
SELECT Id FROM Roles WHERE Name = @roleName;";
                            cmd.Parameters.Add(new SqlParameter("@roleName", SqlDbType.NVarChar, 100) { Value = role });
                            var obj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                            roleId = Convert.ToInt32(obj!);
                        }

                        await using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = "INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId);";
                            cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });
                            cmd.Parameters.Add(new SqlParameter("@roleId", SqlDbType.Int) { Value = roleId });
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                }

                tx.Commit();
                _logger.LogDebug("UserRepository.AddUserAsync executed and roles assigned for user {Username}", username);
                return true;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                _logger.LogError(ex, "Error in AddUserAsync for user {Username}", username);
                return false;
            }
        }

        public async Task<string?> GetHashedPasswordAsync(string username)
        {
            const string sql = "SELECT HashedPassword FROM Users WHERE Username = @username;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });

            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow).ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.IsDBNull(0) ? null : reader.GetString(0);
                _logger.LogDebug("UserRepository.GetHashedPasswordAsync found user '{Username}' present={Present}", username, result != null);
                return result;
            }

            _logger.LogDebug("UserRepository.GetHashedPasswordAsync: user '{Username}' not found", username);
            return null;
        }

        public async Task<string[]?> GetRolesAsync(string username)
        {
            const string sql = @"
SELECT r.Name
FROM Users u
LEFT JOIN UserRoles ur ON u.Id = ur.UserId
LEFT JOIN Roles r ON ur.RoleId = r.Id
WHERE u.Username = @username;";

            var roles = new System.Collections.Generic.List<string>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0)) roles.Add(reader.GetString(0));
            }

            if (roles.Count == 0) return System.Array.Empty<string>();
            return roles.ToArray();
        }

        public async Task<bool> SetRolesAsync(string username, string[] roles)
        {
            // Transactional update: ensure roles exist, clear existing mappings and insert new ones
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);
            var tx = conn.BeginTransaction();
            try
            {
                // Get user id
                int userId;
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "SELECT Id FROM Users WHERE Username = @username;";
                    cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });
                    var obj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                    if (obj == null) { tx.Rollback(); return false; }
                    userId = Convert.ToInt32(obj!);
                }

                // Clear existing mappings
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM UserRoles WHERE UserId = @userId;";
                    cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });
                    await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (roles != null && roles.Length > 0)
                {
                    foreach (var role in roles)
                    {
                        if (string.IsNullOrWhiteSpace(role)) continue;
                        int roleId;
                        await using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = @roleName)
    INSERT INTO Roles (Name) VALUES (@roleName);
SELECT Id FROM Roles WHERE Name = @roleName;";
                            cmd.Parameters.Add(new SqlParameter("@roleName", SqlDbType.NVarChar, 100) { Value = role });
                            var obj = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                            roleId = Convert.ToInt32(obj!);
                        }

                        await using (var cmd = conn.CreateCommand())
                        {
                            cmd.Transaction = tx;
                            cmd.CommandText = "INSERT INTO UserRoles (UserId, RoleId) VALUES (@userId, @roleId);";
                            cmd.Parameters.Add(new SqlParameter("@userId", SqlDbType.Int) { Value = userId });
                            cmd.Parameters.Add(new SqlParameter("@roleId", SqlDbType.Int) { Value = roleId });
                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }
                }

                tx.Commit();
                _logger.LogDebug("UserRepository.SetRolesAsync executed for user {Username}", username);
                return true;
            }
            catch (Exception ex)
            {
                try { tx.Rollback(); } catch { }
                _logger.LogError(ex, "Error in SetRolesAsync for user {Username}", username);
                return false;
            }
        }

        public async Task<bool> UpdatePasswordAsync(string username, string newHashedPassword)
        {
            const string sql = "UPDATE Users SET HashedPassword = @hashedPassword WHERE Username = @username;";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            cmd.Parameters.Add(new SqlParameter("@hashedPassword", SqlDbType.NVarChar, 512) { Value = newHashedPassword });
            cmd.Parameters.Add(new SqlParameter("@username", SqlDbType.NVarChar, 256) { Value = username });

            var rows = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            _logger.LogDebug("UserRepository.UpdatePasswordAsync executed, rowsAffected={Rows}", rows);
            return rows > 0;
        }

        public async Task<System.Collections.Generic.Dictionary<string, string[]>> GetAllUsersAsync()
        {
            const string sql = @"
SELECT u.Username, r.Name
FROM Users u
LEFT JOIN UserRoles ur ON u.Id = ur.UserId
LEFT JOIN Roles r ON ur.RoleId = r.Id;";

            var dict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var username = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var role = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (!dict.TryGetValue(username, out var list))
                {
                    list = new System.Collections.Generic.List<string>();
                    dict[username] = list;
                }
                if (role != null) list.Add(role);
            }

            // Convert to dictionary<string,string[]>
            var result = new System.Collections.Generic.Dictionary<string, string[]>(dict.Count, System.StringComparer.OrdinalIgnoreCase);
            foreach (var kv in dict)
            {
                result[kv.Key] = kv.Value.Count == 0 ? System.Array.Empty<string>() : kv.Value.ToArray();
            }
            return result;
        }

        public async Task<string[]> GetAllRolesAsync()
        {
            const string sql = "SELECT Name FROM Roles ORDER BY Name;";
            var roles = new System.Collections.Generic.List<string>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;

            await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0)) roles.Add(reader.GetString(0));
            }

            return roles.ToArray();
        }

        // Manager mapping is not implemented for the SQL-backed repository in this minimal migration.
        // Return false / null to indicate operation not supported. Consumers should handle this gracefully
        // or use the in-memory repository which supports manager mappings for local/dev scenarios.
        public Task<bool> SetManagerAsync(string username, string managerUsername)
        {
            _logger.LogWarning("SetManagerAsync called on SQL-backed UserRepository but manager mapping is not implemented.");
            return Task.FromResult(false);
        }

        public Task<string[]?> GetUsersForManagerAsync(string managerUsername)
        {
            _logger.LogWarning("GetUsersForManagerAsync called on SQL-backed UserRepository but manager mapping is not implemented.");
            return Task.FromResult<string[]?>(null);
        }
    }
}
