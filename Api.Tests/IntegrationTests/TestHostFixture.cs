using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

[SetUpFixture]
public class TestHostFixture
{
    public static WebApplicationFactory<Program>? Factory;

    [OneTimeSetUp]
    public void GlobalSetup()
    {
        // Create a single shared WebApplicationFactory for all integration tests to speed up host startup.
        Factory = new WebApplicationFactory<Program>();

        // Ensure a clean Identity database for each test run. Deleting/creating the database
        // here avoids test-to-test collisions when using a shared factory. This is a test-only
        // convenience; in production the migrations pipeline should manage schema.
        try
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetService<Api.Identity.ApplicationDbContext>();
            // Delete then recreate the DB so each test run starts from a clean state
            // and then ensure the basic Identity roles and a seeded admin user exist
            // for tests that rely on them.
            db?.Database.EnsureDeleted();
            db?.Database.EnsureCreated();

            try
            {
                var roleManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
                var userManager = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Identity.UserManager<Api.Identity.ApplicationUser>>();

                if (roleManager != null)
                {
                    if (!roleManager.RoleExistsAsync("Admin").GetAwaiter().GetResult())
                        roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Admin")).GetAwaiter().GetResult();
                    if (!roleManager.RoleExistsAsync("User").GetAwaiter().GetResult())
                        roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("User")).GetAwaiter().GetResult();
                    if (!roleManager.RoleExistsAsync("Manager").GetAwaiter().GetResult())
                        roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole("Manager")).GetAwaiter().GetResult();
                }

                // Ensure a predictable seeded admin exists for tests that expect it.
                // Tests use username 'admin' and password 'ChangeMe123!' in several places.
                if (userManager != null)
                {
                    var admin = userManager.FindByNameAsync("admin").GetAwaiter().GetResult();
                    if (admin == null)
                    {
                        var u = new Api.Identity.ApplicationUser { UserName = "admin" };
                        var pw = "ChangeMe123!";
                        var created = userManager.CreateAsync(u, pw).GetAwaiter().GetResult();
                        if (created.Succeeded)
                        {
                            userManager.AddToRoleAsync(u, "Admin").GetAwaiter().GetResult();
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // Don't let role/user setup failures hide test failures; tests will surface real errors.
            }
        }
        catch (System.Exception)
        {
            // If cleanup fails, let tests proceed and surface failures; do not throw here to
            // avoid masking test failures with setup exceptions.
        }
    }

    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        Factory?.Dispose();
        Factory = null;
    }
}
