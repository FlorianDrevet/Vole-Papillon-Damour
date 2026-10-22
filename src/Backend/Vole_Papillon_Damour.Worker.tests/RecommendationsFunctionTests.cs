using System.Reflection;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Vole_Papillon_Damour.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class RecommendationsFunctionTests
{
    [Fact]
    public void RecommendationsFunction_UsesQueueTriggerAndHasNoTimerTrigger()
    {
        var methods = typeof(RecommendationsFunction).GetMethods(BindingFlags.Instance | BindingFlags.Public);
        var queueTriggers = methods
            .SelectMany(method => method.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributes<QueueTriggerAttribute>())
            .ToArray();
        var timerTriggers = methods
            .SelectMany(method => method.GetParameters())
            .SelectMany(parameter => parameter.GetCustomAttributes<TimerTriggerAttribute>())
            .ToArray();

        queueTriggers.Should().ContainSingle(trigger => trigger.QueueName == "book-recommendations");
        timerTriggers.Should().BeEmpty();
    }
}
