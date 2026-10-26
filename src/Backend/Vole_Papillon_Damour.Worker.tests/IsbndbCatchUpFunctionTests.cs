using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Vole_Papillon_Damour.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class IsbndbCatchUpFunctionTests
{
    [Fact]
    public void CatchUp_timer_uses_configured_schedule()
    {
        var runMethod = typeof(IsbndbCatchUpFunction).GetMethod(nameof(IsbndbCatchUpFunction.Run))
            ?? throw new InvalidOperationException("The ISBNdb catch-up timer function was not found.");
        var timerTrigger = runMethod.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<TimerTriggerAttribute>())
            .Should().ContainSingle().Subject;

        timerTrigger.Schedule.Should().Be("%Isbndb:CatchUpSchedule%");
    }

    [Fact]
    public void Manual_catch_up_requires_a_function_key_and_post()
    {
        var method = typeof(IsbndbCatchUpFunction).GetMethod(nameof(IsbndbCatchUpFunction.RunManually))
            ?? throw new InvalidOperationException("The manual ISBNdb catch-up function was not found.");
        var trigger = method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>())
            .Should().ContainSingle().Subject;

        trigger.AuthLevel.Should().Be(AuthorizationLevel.Function);
        trigger.Methods.Should().ContainSingle().Which.Should().Be("post");
        method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<DurableClientAttribute>())
            .Should().ContainSingle();
    }

    [Fact]
    public void Manual_catch_up_orchestration_calls_an_activity()
    {
        var method = typeof(IsbndbCatchUpOrchestrationFunction).GetMethod(
                nameof(IsbndbCatchUpOrchestrationFunction.Run))
            ?? throw new InvalidOperationException("The ISBNdb catch-up orchestration was not found.");

        method.GetCustomAttributes<FunctionAttribute>()
            .Should().ContainSingle(attribute => attribute.Name == "IsbndbCatchUpOrchestration");
        method.GetParameters()
            .SelectMany(parameter => parameter.GetCustomAttributes<OrchestrationTriggerAttribute>())
            .Should().ContainSingle();
    }
}
