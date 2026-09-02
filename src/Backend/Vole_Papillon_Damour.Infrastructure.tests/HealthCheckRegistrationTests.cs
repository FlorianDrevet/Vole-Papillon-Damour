using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Infrastructure.Health;

namespace Vole_Papillon_Damour.Infrastructure.tests;

public class HealthCheckRegistrationTests
{
    [Fact]
    public void AddInfrastructureHealthChecks_RegistersDatabaseCheck()
    {
        var services = new ServiceCollection();

        services
            .AddHealthChecks()
            .AddInfrastructureHealthChecks();

        using var provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations
            .Should()
            .ContainSingle(registration => registration.Name == "database");
    }
}
