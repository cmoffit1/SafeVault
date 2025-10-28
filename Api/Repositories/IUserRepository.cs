using System.Threading.Tasks;

namespace Api.Repositories
{
    public interface IUserRepository
    {
        /// <summary>
        /// Adds a new user with the provided username and hashed password.
        /// Implementations should ensure the username is used as a key and return
        /// <c>true</c> when the user was added successfully, or <c>false</c> if the
        /// user already exists or an error occurred.
        /// </summary>
        /// <param name="username">Normalized username/key.</param>
        /// <param name="hashedPassword">The hashed password blob produced by <see cref="Api.Utilities.IPasswordHasher"/>.</param>
    /// <summary>
    /// Adds a new user with the provided username and hashed password.
    /// Implementations should ensure the username is used as a key and return
    /// <c>true</c> when the user was added successfully, or <c>false</c> if the
    /// user already exists or an error occurred.
    /// </summary>
    /// <param name="username">Normalized username/key.</param>
    /// <param name="hashedPassword">The hashed password blob produced by <see cref="Api.Utilities.IPasswordHasher"/>.</param>
    Task<bool> AddUserAsync(string username, string hashedPassword);

    /// <summary>
    /// Adds a new user with roles.
    /// </summary>
    Task<bool> AddUserAsync(string username, string hashedPassword, string[] roles);

        /// <summary>
        /// Retrieves the stored hashed password for the given username or <c>null</c>
        /// when the user does not exist.
        /// </summary>
        /// <param name="username">Normalized username/key to look up.</param>
        Task<string?> GetHashedPasswordAsync(string username);

        /// <summary>
        /// Retrieves the roles assigned to the specified username. Returns empty array when none or null if user not found.
        /// </summary>
        Task<string[]?> GetRolesAsync(string username);

        /// <summary>
        /// Sets the roles for the specified username. Returns true when update succeeds or false when user not found.
        /// </summary>
        Task<bool> SetRolesAsync(string username, string[] roles);

        /// <summary>
        /// Updates the stored password hash for an existing user. Returns true when update succeeded.
        /// </summary>
        Task<bool> UpdatePasswordAsync(string username, string newHashedPassword);

        /// <summary>
        /// Returns a map of usernames to assigned roles. Used by admin endpoints.
        /// </summary>
        Task<System.Collections.Generic.Dictionary<string, string[]>> GetAllUsersAsync();

    /// <summary>
    /// Returns the list of all role names available in the system.
    /// </summary>
    Task<string[]> GetAllRolesAsync();

        /// <summary>
        /// Assign a manager for a user. Returns true when update succeeded.
        /// </summary>
        Task<bool> SetManagerAsync(string username, string managerUsername);

        /// <summary>
        /// Returns all usernames assigned to the given manager.
        /// </summary>
        Task<string[]?> GetUsersForManagerAsync(string managerUsername);
    }
}
