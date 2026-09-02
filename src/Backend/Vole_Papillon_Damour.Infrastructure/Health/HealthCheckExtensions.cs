using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vole_Papillon_Damour.Infrastructure.Health;

public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddInfrastructureHealthChecks(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name);
    }
}
