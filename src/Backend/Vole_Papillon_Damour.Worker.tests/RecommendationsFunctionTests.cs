using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;
using Vole_Papillon_Damour.Worker;
using Xunit;

namespace Vole_Papillon_Damour.Worker.Tests;

public sealed class RecommendationsFunctionTests
{
    [Fact]
    public async Task Run_SendsProfileRefreshBeforeNeighborComputationAndLogsCounters()
    {
        var sender = DispatchProxy.Create<ISender, RecordingSenderProxy>();
        var senderProxy = sender as RecordingSenderProxy
                          ?? throw new InvalidOperationException("Could not create the recording sender.");
        senderProxy.Responses[typeof(RefreshBookSimilarityProfilesResult)] =
            new RefreshBookSimilarityProfilesResult(4, 3, 1);
        senderProxy.Responses[typeof(RecomputeBookNeighborsResult)] =
            new RecomputeBookNeighborsResult(3, 2, 6, Guid.NewGuid());
        var services = new ServiceCollection().AddSingleton(sender);
        using var rootProvider = services.BuildServiceProvider();
        var logger = new RecordingLogger<RecommendationsFunction>();
        var function = new RecommendationsFunction(
            rootProvider.GetRequiredService<IServiceScopeFactory>(),
            logger);

        await function.Run(null!, CancellationToken.None);

        senderProxy.Requests.Select(request => request.GetType()).Should().Equal(
            typeof(RefreshBookSimilarityProfilesCommand),
            typeof(RecomputeBookNeighborsCommand));
        logger.Messages.Should().HaveCount(2);
        logger.Messages[0].Should().Contain("Candidates: 4").And.Contain("Found: 3").And.Contain("NotFound: 1");
        logger.Messages[1].Should().Contain("Books: 3").And.Contain("Embedded: 2").And.Contain("Neighbors: 6");

        var schedule = typeof(RecommendationsFunction).GetMethod(nameof(RecommendationsFunction.Run))!
            .GetParameters()
            .Single(parameter => parameter.GetCustomAttributes(typeof(TimerTriggerAttribute), false).Length == 1)
            .GetCustomAttribute<TimerTriggerAttribute>()!
            .Schedule;
        schedule.Should().Be("0 30 3 * * *");
    }
}

internal class RecordingSenderProxy : DispatchProxy
{
    public List<object> Requests { get; } = [];
    public Dictionary<Type, object> Responses { get; } = [];

    protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null || targetMethod.Name != nameof(ISender.Send) || !targetMethod.IsGenericMethod)
        {
            throw new NotSupportedException($"ISender method {targetMethod?.Name} is not used by this test.");
        }

        Requests.Add(args![0]!);
        var responseType = targetMethod.GetGenericArguments()[0];
        var response = Responses[responseType];
        return typeof(RecordingSenderProxy)
            .GetMethod(nameof(CreateTask), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(responseType)
            .Invoke(null, [response])!;
    }

    private static Task<TResponse> CreateTask<TResponse>(object response) => Task.FromResult((TResponse)response);
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}
