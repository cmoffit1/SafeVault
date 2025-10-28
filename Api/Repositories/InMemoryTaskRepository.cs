using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Repositories
{
    public class InMemoryTaskRepository : ITaskRepository
    {
        private readonly ConcurrentDictionary<int, Api.Models.TaskItem> _tasks = new();
        private int _nextId = 1;

        public Task<Api.Models.TaskItem> AddTaskAsync(Api.Models.TaskItem item)
        {
            var id = System.Threading.Interlocked.Increment(ref _nextId);
            item.Id = id;
            item.CreatedAt = System.DateTime.UtcNow;
            _tasks[id] = item;
            return Task.FromResult(item);
        }

        public Task<Api.Models.TaskItem?> GetTaskByIdAsync(int id)
        {
            _tasks.TryGetValue(id, out var item);
            return Task.FromResult(item);
        }

        public Task<bool> MarkCompleteAsync(int id, string completedBy)
        {
            if (!_tasks.TryGetValue(id, out var item)) return Task.FromResult(false);
            if (item.Completed) return Task.FromResult(false);
            item.Completed = true;
            item.CompletedAt = System.DateTime.UtcNow;
            _tasks[id] = item;
            return Task.FromResult(true);
        }

        public Task<bool> MarkInProgressAsync(int id, string startedBy)
        {
            if (!_tasks.TryGetValue(id, out var item)) return Task.FromResult(false);
            // if already completed, can't start
            if (item.Completed) return Task.FromResult(false);
            // set in-progress flag
            item.InProgress = true;
            _tasks[id] = item;
            return Task.FromResult(true);
        }

        public Task<Api.Dtos.TaskDto[]> GetTasksForUserAsync(string username)
        {
            var key = username.ToLowerInvariant();
            var list = _tasks.Values
                .Where(t => t.AssigneeUsername.Equals(key, System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.CreatedAt)
                .Select(t => new Api.Dtos.TaskDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    Creator = t.CreatorUsername,
                    Assignee = t.AssigneeUsername,
                    InProgress = t.InProgress,
                    Completed = t.Completed,
                    CreatedAt = t.CreatedAt,
                    CompletedAt = t.CompletedAt
                })
                .ToArray();
            return Task.FromResult(list);
        }

        public Task<bool> ReassignTaskAsync(int id, string newAssignee, string changedBy)
        {
            if (!_tasks.TryGetValue(id, out var item)) return Task.FromResult(false);
            var copy = item;
            copy.AssigneeUsername = newAssignee;
            _tasks[id] = copy;
            return Task.FromResult(true);
        }
    }
}
