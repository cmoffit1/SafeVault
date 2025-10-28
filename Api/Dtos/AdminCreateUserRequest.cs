namespace Api.Dtos
{
    public class AdminCreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string[]? Roles { get; set; }
    }
}
