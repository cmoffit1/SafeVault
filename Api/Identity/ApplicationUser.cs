using Microsoft.AspNetCore.Identity;

namespace Api.Identity
{
    public class ApplicationUser : IdentityUser
    {
        // Optional: store manager mapping directly on the user record
        public string? ManagerUsername { get; set; }
    }
}
