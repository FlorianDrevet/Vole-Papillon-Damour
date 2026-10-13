using Vole_Papillon_Damour.Api.Common.Mapping;
using Vole_Papillon_Damour.Api.LiveTableau;

namespace Vole_Papillon_Damour.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddMapping();
        services.AddAuthorization();
        services.AddLiveTableau();
        return services;
    }
}
