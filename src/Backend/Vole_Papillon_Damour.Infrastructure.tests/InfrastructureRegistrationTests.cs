using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Infrastructure.Services.BookAlerts;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;
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

    [Fact]
    public void AddInfrastructure_RegistersTheBookAlertOutbox()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddInfrastructure(
            configuration,
            runMigrations: false,
            registerAuthentication: false);

        using var provider = services.BuildServiceProvider();

        provider.GetService<IBookAlertOutbox>().Should().BeOfType<BookAlertOutbox>();
    }

    [Fact]
    public void AddBookMetadataEnrichmentProcessing_RegistersTheQueueAndWorker()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationManager();

        services.AddInfrastructure(
            configuration,
            runMigrations: false,
            registerAuthentication: false);
        services.AddBookMetadataEnrichmentProcessing();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IBookMetadataEnrichmentQueue>()
            .Should().BeOfType<BookMetadataEnrichmentQueue>();
        provider.GetServices<IHostedService>()
            .Should().ContainSingle(service =>
                service.GetType() == typeof(BookMetadataEnrichmentBackgroundService));
    }
}
