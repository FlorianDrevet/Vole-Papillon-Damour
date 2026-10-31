using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace ShopAppVpd.Apis;

/// <summary>Provides dependency-injection registration for the VPD API.</summary>
public static class VpdApiServiceCollectionExtensions
{
    /// <summary>Registers the public VPD API client without authentication.</summary>
    /// <param name="services">The service collection to update.</param>
    /// <param name="baseAddress">The deployed API base address.</param>
    /// <returns>A Refit client builder for the public API.</returns>
    public static IHttpClientBuilder AddUnauthenticatedVpdApi(
        this IServiceCollection services,
        Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("The API base address must be absolute.", nameof(baseAddress));
        }

        return services
            .AddRefitClient<IVpdApi>()
            .ConfigureHttpClient(client => client.BaseAddress = baseAddress);
    }
}
