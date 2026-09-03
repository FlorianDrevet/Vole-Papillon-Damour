using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Infrastructure;

namespace Vole_Papillon_Damour.Infrastructure.tests;

public class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_WhenAuthenticationIsDisabled_DoesNotRegisterAuthenticationSchemes()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddInfrastructure(
            configuration,
            runMigrations: false,
            registerAuthentication: false);

        using var provider = services.BuildServiceProvider();

        provider.GetService<IAuthenticationSchemeProvider>().Should().BeNull();
    }
}
