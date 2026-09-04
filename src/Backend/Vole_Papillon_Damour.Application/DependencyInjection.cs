using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Vole_Papillon_Damour.Application.Common.Behaviors;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.AccountDeletion;

namespace Vole_Papillon_Damour.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAccountDeletionProcessing();
        services.AddScoped<BookAlertDeliveryService>();
        services.AddScoped<MemberIdentityService>();
        // The background inactivity sweep delegates to the same close-session
        // handler as the HTTP path. Registering the concrete handler keeps the
        // shared domain transaction and makes design-time EF tooling able to
        // validate the complete application graph.
        services.AddScoped<Books.Commands.ScanSession.CloseScanSessionCommandHandler>();

        // CQRS with MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(DependencyInjection).Assembly, Assembly.GetExecutingAssembly()));
        
        // Behaviors
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        
        // Validators
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IServiceCollection AddAccountDeletionProcessing(this IServiceCollection services)
    {
        services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        return services;
    }
}
