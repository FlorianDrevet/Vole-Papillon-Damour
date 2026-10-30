using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class WorkerTriggerScheduleTests
{
    [Fact]
    public void Host_logging_keeps_function_summaries_and_warnings_without_default_information()
    {
        var hostJsonPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../Vole_Papillon_Damour.Worker/host.json"));
        using var hostJson = JsonDocument.Parse(File.ReadAllText(hostJsonPath));
        var logLevels = hostJson.RootElement
            .GetProperty("logging")
            .GetProperty("logLevel");

        logLevels.GetProperty("default").GetString().Should().Be("Warning");
        logLevels.GetProperty("Host.Aggregator").GetString().Should().Be("Warning");
        logLevels.GetProperty("Host.Results").GetString().Should().Be("Information");

        foreach (var functionName in new[]
        {
            "Enrich",
            "ResumeEnrich",
            "EnrichCatalogBatch",
            "Sweep",
            "IsbndbCatchUp",
            "IsbndbCatchUpManual",
            "RecommendationsBackfill",
            "ProcessBookRecommendations",
            "ImportSocialActualities"
        })
        {
            logLevels.GetProperty($"Function.{functionName}.User")
                .GetString()
                .Should()
                .Be("Information");
        }
    }

    [Fact]
    public void Sweep_runs_once_per_hour()
    {
        GetSchedule<BookSweepFunction>().Should().Be("0 0 * * * *");
    }

    [Fact]
    public void Enrichment_is_started_manually_with_a_function_key()
    {
        var runMethod = typeof(BookEnrichmentFunction).GetMethod(nameof(BookEnrichmentFunction.Run))
            ?? throw new InvalidOperationException($"Run method not found on {nameof(BookEnrichmentFunction)}.");
        var attributes = runMethod.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes());
        var httpTrigger = attributes.Single(attribute => attribute.GetType().Name == "HttpTriggerAttribute");

        httpTrigger.GetType().GetProperty("AuthLevel")!.GetValue(httpTrigger)!.ToString()
            .Should().Be("Function");
        ((IEnumerable<string>)httpTrigger.GetType().GetProperty("Methods")!.GetValue(httpTrigger)!)
            .Should().ContainSingle("post");
        runMethod.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes())
            .Should().NotContain(attribute => attribute.GetType().Name == "TimerTriggerAttribute");
    }

    [Fact]
    public void Enrichment_resume_requires_a_function_key_and_uses_a_dedicated_route()
    {
        var resumeMethod = typeof(BookEnrichmentFunction).GetMethod(nameof(BookEnrichmentFunction.Resume))
            ?? throw new InvalidOperationException($"Resume method not found on {nameof(BookEnrichmentFunction)}.");
        var httpTrigger = resumeMethod.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes())
            .Single(attribute => attribute.GetType().Name == "HttpTriggerAttribute");

        httpTrigger.GetType().GetProperty("AuthLevel")!.GetValue(httpTrigger)!.ToString()
            .Should().Be("Function");
        httpTrigger.GetType().GetProperty("Route")!.GetValue(httpTrigger)
            .Should().Be("Enrich/resume");
        ((IEnumerable<string>)httpTrigger.GetType().GetProperty("Methods")!.GetValue(httpTrigger)!)
            .Should().ContainSingle("post");
    }

    [Fact]
    public void Enrichment_resume_request_requires_the_refresh_timestamp_and_completed_batch()
    {
        var deserialize = () => JsonSerializer.Deserialize<EnrichCatalogResumeRequest>("{}");

        deserialize.Should().Throw<JsonException>();
    }

    [Fact]
    public void Social_import_uses_the_configured_schedule()
    {
        GetSchedule<SocialImportFunction>().Should().Be("%SocialImport:Schedule%");
    }

    private static string GetSchedule<TFunction>()
    {
        var runMethod = typeof(TFunction).GetMethod(nameof(BookSweepFunction.Run))
            ?? throw new InvalidOperationException($"Run method not found on {typeof(TFunction).Name}.");
        var timerParameter = runMethod.GetParameters()
            .Single(parameter => parameter.GetCustomAttributes(typeof(TimerTriggerAttribute), false).Length == 1);
        var timerTrigger = timerParameter.GetCustomAttribute<TimerTriggerAttribute>()
            ?? throw new InvalidOperationException($"Timer trigger not found on {typeof(TFunction).Name}.");

        return timerTrigger.Schedule;
    }
}
