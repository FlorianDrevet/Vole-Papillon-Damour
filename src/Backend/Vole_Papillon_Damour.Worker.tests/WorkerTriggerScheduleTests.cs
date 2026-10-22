using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class WorkerTriggerScheduleTests
{
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
