using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Application.AccountDeletion;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddAccountDeletionProcessing_RegistersOnlyTheAccountDeletionService()
    {
        var services = new ServiceCollection();

        services.AddAccountDeletionProcessing();

        services
            .Should()
            .ContainSingle(descriptor =>
                descriptor.ServiceType == typeof(IAccountDeletionService)
                && descriptor.ImplementationType == typeof(AccountDeletionService));
        services
            .Should()
            .NotContain(descriptor => descriptor.ServiceType == typeof(MediatR.IMediator));
    }
}
