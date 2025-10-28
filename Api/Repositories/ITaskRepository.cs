using System.Threading.Tasks;

namespace Api.Repositories
{
    public interface ITaskRepository
    {
        Task<Api.Models.TaskItem> AddTaskAsync(Api.Models.TaskItem item);
        Task<Api.Models.TaskItem?> GetTaskByIdAsync(int id);
        Task<bool> MarkCompleteAsync(int id, string completedBy);
        Task<bool> MarkInProgressAsync(int id, string startedBy);
        Task<bool> ReassignTaskAsync(int id, string newAssignee, string changedBy);
        Task<Api.Dtos.TaskDto[]> GetTasksForUserAsync(string username);
    }
}
