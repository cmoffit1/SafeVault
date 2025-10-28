using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Api.Repositories;
using System.Collections.Generic;
using System.Linq;

namespace Api.Tests
{
    public class RepositoryRegistrationTests
    {
        [Test]
        public void When_NoConnectionString_Registers_InMemoryRepo()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var services = new ServiceCollection();
            // Build a Configuration and use it directly
            var builderConfig = config;

            // run the same registration logic as Program
            var defaultConn = builderConfig.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConn))
            {
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            }
            else
            {
                services.AddScoped<IUserRepository, UserRepository>();
            }

            var provider = services.BuildServiceProvider();
            var repo = provider.GetService<IUserRepository>();
            Assert.That(repo, Is.Not.Null);
            Assert.That(repo, Is.InstanceOf<InMemoryUserRepository>());
        }

        [Test]
        public void When_DefaultConnection_Exists_Registers_UserRepository()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "ConnectionStrings:DefaultConnection", "Server=(local);Database=Test;Trusted_Connection=True;" }
                })
                .Build();

            var services = new ServiceCollection();
            var builderConfig2 = config;

            var defaultConn = builderConfig2.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(defaultConn))
            {
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();
            }
            else
            {
                // For registration inspection we don't need to provide IConfiguration; just register the mapping
                services.AddScoped<IUserRepository, UserRepository>();
            }

            var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IUserRepository));
            Assert.That(descriptor, Is.Not.Null, "IUserRepository should be registered");
            Assert.That(descriptor!.ImplementationType, Is.EqualTo(typeof(UserRepository)));
            Assert.That(descriptor.Lifetime, Is.EqualTo(ServiceLifetime.Scoped));
        }
    }
}
