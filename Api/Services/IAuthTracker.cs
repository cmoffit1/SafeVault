using System.Threading.Tasks;

namespace Api.Services
{
    /// <summary>
    /// Tracks authentication attempts and lockout state for usernames or keys.
    /// Implementations should be thread-safe and durable as needed.
    /// </summary>
    public interface IAuthTracker
    {
        /// <summary>
        /// Record a failed authentication attempt for a given key (username or IP).
        /// Returns the number of consecutive failed attempts after recording.
        /// </summary>
        Task<int> RecordFailureAsync(string key);

        /// <summary>
        /// Resets the failure count for the given key (on successful auth).
        /// </summary>
        Task ResetAsync(string key);

        /// <summary>
        /// Returns whether the key is currently locked out.
        /// </summary>
        Task<bool> IsLockedOutAsync(string key);
    }
}
