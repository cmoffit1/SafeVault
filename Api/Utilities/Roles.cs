namespace Api.Utilities
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string User = "User";
        public const string Manager = "Manager";

        public static readonly string[] All = new[] { Admin, User, Manager };
    }
}
