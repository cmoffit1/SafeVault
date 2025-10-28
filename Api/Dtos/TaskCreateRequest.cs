namespace Api.Dtos
{
    public class TaskCreateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string AssigneeUsername { get; set; } = string.Empty;
    }
}
