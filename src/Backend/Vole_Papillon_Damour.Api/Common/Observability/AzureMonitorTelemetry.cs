using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Vole_Papillon_Damour.Api.Common.Observability;

/// <summary>
/// Explicit Azure Monitor settings for the API host.
/// </summary>
public static class AzureMonitorTelemetry
{
    /// <summary>
    /// Exports every trace. Azure.Monitor.OpenTelemetry.AspNetCore 1.5 silently switched
    /// its default to a 5 traces/s rate limiter, which drops the scan bursts and the rare
    /// slow request we most need to see. Volume stays low (about 2.5 req/s at peak), so the
    /// cost lever is the daily cap, never sampling (11-observabilite.md §5).
    /// </summary>
    /// <param name="options">The distro options to configure.</param>
    public static void ConfigureSampling(AzureMonitorOptions options)
    {
        options.SamplingRatio = 1.0F;
        options.TracesPerSecond = null;
    }
}
