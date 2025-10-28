using System;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

class Program
{
    static async System.Threading.Tasks.Task<int> Main()
    {
        // Set token settings as tests do
        var signingKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test-signing-key-which-should-be-long-enough"));
        Environment.SetEnvironmentVariable("TokenSettings__SigningKey", signingKey);
        Environment.SetEnvironmentVariable("TokenSettings__Issuer", "SafeVault");
        Environment.SetEnvironmentVariable("TokenSettings__Audience", "SafeVaultClients");

    var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<Api.Repositories.IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<Api.Utilities.IPasswordHasher>();

        var adminUser = "reproadmin";
        var adminPass = "AdminList123!";
        var hashed = hasher.Hash(adminPass);
        var added = await repo.AddUserAsync(adminUser, hashed);
        Console.WriteLine($"Added user: {added}");
        var okRoles = await repo.SetRolesAsync(adminUser, new[] { "Admin" });
        Console.WriteLine($"Set roles ok: {okRoles}");

        var client = factory.CreateClient();
        var authResp = await client.PostAsJsonAsync("/authenticate", new { Username = adminUser, Password = adminPass });
        Console.WriteLine($"Authenticate status: {authResp.StatusCode}");
        var content = await authResp.Content.ReadFromJsonAsync<System.Collections.Generic.Dictionary<string, string>>();
        if (content == null || !content.ContainsKey("token"))
        {
            Console.WriteLine("No token returned");
            return 2;
        }
        var token = content["token"];
        Console.WriteLine($"Token: {token}");

        // Decode token payload
        try
        {
            var parts = token.Split('.');
            if (parts.Length >= 2)
            {
                var payload = parts[1];
                var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
                var json = System.Text.Encoding.UTF8.GetString(bytes);
                Console.WriteLine("Token payload: " + json);
            }
        }
        catch (Exception ex) { Console.WriteLine("Failed to decode token: " + ex.Message); }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var usersResp = await client.GetAsync("/admin/users");
        Console.WriteLine($"GET /admin/users => {usersResp.StatusCode}");
        var text = await usersResp.Content.ReadAsStringAsync();
        Console.WriteLine("Body: " + text);

        return 0;
    }
}
