using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
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
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;
using Vole_Papillon_Damour.Worker;

const int defaultWorkerBnfTimeoutMilliseconds = 2_000;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureLogging((context, logging) => WorkerLoggingConfiguration.Configure(
        logging,
        context.HostingEnvironment.IsProduction()))
    .ConfigureServices((context, services) =>
    {
        services
            .AddApplication()
            .AddInfrastructure(
                context.Configuration,
                runMigrations: false,
                registerAuthentication: false,
                registerMemberCardTokens: false);

        var workerBnfTimeoutMilliseconds = context.Configuration.GetValue<int?>(
            "Bibliographic:WorkerBnfTimeoutMilliseconds") ?? defaultWorkerBnfTimeoutMilliseconds;
        if (workerBnfTimeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException(
                "Bibliographic:WorkerBnfTimeoutMilliseconds must be greater than zero.");
        }

        // Keep the API's request latency budget unchanged while giving long-running
        // catalog enrichment more time than the observed 800 ms BnF timeout tail.
        services.PostConfigure<BibliographicOptions>(options =>
            options.BnfTimeoutMilliseconds = workerBnfTimeoutMilliseconds);

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
                    .AddSource(BookRecommendationTelemetry.ActivitySourceName)
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
