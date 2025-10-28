using System;
using System.Threading.Tasks;

namespace Client.Services
{
    public interface IAuthService
    {
        Task InitializeAsync();
        Task<bool> AuthenticateAsync(string username, string password);
        Task LogoutAsync();
        string? Token { get; }
        string? Username { get; }
        event Action? AuthenticationStateChanged;
    }
}
