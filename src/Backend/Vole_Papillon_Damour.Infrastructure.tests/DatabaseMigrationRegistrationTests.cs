using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vole_Papillon_Damour.Infrastructure;

namespace Vole_Papillon_Damour.Infrastructure.tests;

public class DatabaseMigrationRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersDatabaseMigrationHostedService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service.GetType().Name.StartsWith("MigrationHostedService"));
    }

    [Fact]
    public void AddInfrastructure_WhenMigrationsAreDisabled_DoesNotRegisterDatabaseMigrationHostedService()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddInfrastructure(configuration, runMigrations: false);

        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<IHostedService>()
            .Should()
            .NotContain(service => service.GetType().Name.StartsWith("MigrationHostedService"));
    }
}
