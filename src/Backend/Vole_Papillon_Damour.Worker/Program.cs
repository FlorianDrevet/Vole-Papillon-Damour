using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Vole_Papillon_Damour.Application;
using Vole_Papillon_Damour.Application.Common.Observability;
using Vole_Papillon_Damour.Infrastructure;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging(logging =>
    {
        // SQL statements carry parameter values and are the first useless ingestion
        // item (11-observabilite.md §6); per-request HttpClient logs duplicate the
        // dependency spans.
        logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
    })
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplication()
            .AddInfrastructure(
                context.Configuration,
                runMigrations: false,
                registerAuthentication: false);

        if (!string.IsNullOrWhiteSpace(context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            // host.json sets telemetryMode=OpenTelemetry, so host invocations and these
            // worker spans share one trace. The distro is deliberately not used here: its
            // ASP.NET Core instrumentation would duplicate the host request telemetry.
            services
                .AddOpenTelemetry()
                .UseFunctionsWorkerDefaults()
                .WithTracing(tracing => tracing
                    .AddSource(BookScanTelemetry.ActivitySourceName)
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation())
                .WithMetrics(metrics => metrics.AddMeter(BookScanTelemetry.MeterName))
                .UseAzureMonitorExporter(options =>
                {
                    // Same rule as the API: every sweep and enrichment trace is kept.
                    options.SamplingRatio = 1.0F;
                    options.TracesPerSecond = null;
                });

            // Alert rules match AppTraces.Message prefixes such as "Worker sweep completed".
            services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeFormattedMessage = true);
        }
    })
    .Build();

await host.RunAsync();
