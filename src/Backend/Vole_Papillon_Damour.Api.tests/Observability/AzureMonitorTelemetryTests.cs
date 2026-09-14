using Azure.Monitor.OpenTelemetry.AspNetCore;
using FluentAssertions;
using Vole_Papillon_Damour.Api.Common.Observability;

namespace Vole_Papillon_Damour.Api.tests.Observability;

public sealed class AzureMonitorTelemetryTests
{
    [Fact]
    public void ConfigureSampling_ExportsEveryTrace()
    {
        // Distro 1.5 defaults to a 5 traces/s rate limiter; a scan burst would lose
        // exactly the traces documented as mandatory in 11-observabilite.md §5.
        var options = new AzureMonitorOptions();

        AzureMonitorTelemetry.ConfigureSampling(options);

        options.SamplingRatio.Should().Be(1.0F);
        options.TracesPerSecond.Should().BeNull();
    }
}
