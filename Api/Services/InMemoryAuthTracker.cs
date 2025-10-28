using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Api.Services
{
    /// <summary>
    /// Simple in-memory auth tracker for failed attempts and lockout. Not suitable for multi-node deployments.
    /// </summary>
    public class InMemoryAuthTracker : IAuthTracker
    {
        private record Entry(int Count, DateTimeOffset FirstFailureUtc);

        private readonly ConcurrentDictionary<string, Entry> _map = new();
        private readonly int _maxFailures;
        private readonly TimeSpan _lockoutWindow;

        public InMemoryAuthTracker(int maxFailures = 5, TimeSpan? lockoutWindow = null)
        {
            _maxFailures = maxFailures;
            _lockoutWindow = lockoutWindow ?? TimeSpan.FromMinutes(15);
        }

        public Task<int> RecordFailureAsync(string key)
        {
            var now = DateTimeOffset.UtcNow;
            _map.AddOrUpdate(key, k => new Entry(1, now), (k, old) =>
            {
                // If first failure was long ago, reset window
                if (now - old.FirstFailureUtc > _lockoutWindow)
                    return new Entry(1, now);
                return new Entry(old.Count + 1, old.FirstFailureUtc);
            });

            var current = _map[key];
            return Task.FromResult(current.Count);
        }

        public Task ResetAsync(string key)
        {
            _map.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<bool> IsLockedOutAsync(string key)
        {
            if (!_map.TryGetValue(key, out var entry)) return Task.FromResult(false);
            var now = DateTimeOffset.UtcNow;
            if (now - entry.FirstFailureUtc > _lockoutWindow)
            {
                // Window expired; reset
                _map.TryRemove(key, out _);
                return Task.FromResult(false);
            }

            return Task.FromResult(entry.Count >= _maxFailures);
        }
    }
}
