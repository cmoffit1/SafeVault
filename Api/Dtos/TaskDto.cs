using System;

namespace Api.Dtos
{
    public class TaskDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Creator { get; set; } = string.Empty;
        public string Assignee { get; set; } = string.Empty;
        public bool InProgress { get; set; }
        public bool Completed { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
