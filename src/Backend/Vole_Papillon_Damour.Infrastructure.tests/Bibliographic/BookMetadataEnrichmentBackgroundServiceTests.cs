using System.Diagnostics;
using System.Collections.Concurrent;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BookMetadataEnrichmentBackgroundServiceTests
{
    [Fact]
    public async Task StartAsync_WhenAnIsbnIsQueued_SendsTargetedEnrichmentCommand()
    {
        var isbn13 = ParseIsbn("9791036377426");
        var queue = new BookMetadataEnrichmentQueue();
        var sender = Substitute.For<ISender>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.Send(
                Arg.Any<EnrichPendingBooksCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                processed.TrySetResult(true);
                return Task.FromResult(new EnrichPendingBooksResult(1, 1, 0, 0));
            });
        using var provider = new ServiceCollection()
            .AddSingleton<ISender>(sender)
            .BuildServiceProvider();
        var service = new BookMetadataEnrichmentBackgroundService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookMetadataEnrichmentBackgroundService>.Instance);
        using var stopping = new CancellationTokenSource();

        queue.Enqueue(isbn13);
        await service.StartAsync(stopping.Token);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        stopping.Cancel();
        await service.StopAsync(CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<EnrichPendingBooksCommand>(command =>
                command.Isbn13.HasValue && command.Isbn13.Value == isbn13),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenMultipleIsbnsAreQueued_WaitsOneSecondBetweenEnrichmentRequests()
    {
        var queue = new BookMetadataEnrichmentQueue();
        var sender = Substitute.For<ISender>();
        var processedTimes = new ConcurrentQueue<long>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.Send(
                Arg.Any<EnrichPendingBooksCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                processedTimes.Enqueue(Stopwatch.GetTimestamp());
                if (processedTimes.Count == 2)
                {
                    processed.TrySetResult(true);
                }

                return Task.FromResult(new EnrichPendingBooksResult(1, 1, 0, 0));
            });
        using var provider = new ServiceCollection()
            .AddSingleton<ISender>(sender)
            .BuildServiceProvider();
        var service = new BookMetadataEnrichmentBackgroundService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BookMetadataEnrichmentBackgroundService>.Instance);
        using var stopping = new CancellationTokenSource();

        queue.Enqueue(ParseIsbn("9791036377426"));
        queue.Enqueue(ParseIsbn("9783140464079"));
        await service.StartAsync(stopping.Token);
        await processed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        stopping.Cancel();
        await service.StopAsync(CancellationToken.None);

        var times = processedTimes.ToArray();
        times.Should().HaveCount(2);
        Stopwatch.GetElapsedTime(times[0], times[1]).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    private static Isbn13 ParseIsbn(string value)
    {
        return Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
    }
}
