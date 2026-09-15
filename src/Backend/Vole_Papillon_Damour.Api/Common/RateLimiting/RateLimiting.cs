using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Vole_Papillon_Damour.Api.Common.RateLimiting;

public static class DependencyInjection
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("Login", opt =>
            {
                opt.Window = TimeSpan.FromSeconds(10);
                opt.PermitLimit = 3;
            });

            // Both endpoints below are anonymous and each scan or search can
            // trigger up to three outbound calls to BnF / Open Library / Google
            // Books. A per-client window keeps a single misbehaving client (bot,
            // stuck retry loop) from exhausting the association's welcome on
            // those providers without throttling every other volunteer at once.
            options.AddPolicy(RateLimitingPolicies.BibliographicMetadata, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientPartitionKey(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                    }));

            options.AddPolicy(RateLimitingPolicies.BibliographicSearch, httpContext =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    ClientPartitionKey(httpContext),
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 4,
                        QueueLimit = 0,
                    }));
        });
        return services;
    }

    private static string ClientPartitionKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
