using System.Collections.Generic;

namespace Api.Tests
{
    public class TestUserRepository
    {
        private readonly List<(string Username, string Email)> _users = new();

        public void AddUser(string username, string email)
        {
            _users.Add((username, email));
        }

        public (string Username, string Email)? GetUserByUsername(string username)
        {
            foreach (var user in _users)
            {
                if (user.Username == username)
                    return user;
            }
            return null;
        }
    }
}
