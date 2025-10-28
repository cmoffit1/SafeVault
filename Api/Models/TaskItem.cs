using System;

namespace Api.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatorUsername { get; set; } = string.Empty;
        public string AssigneeUsername { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool InProgress { get; set; }
    }
}
