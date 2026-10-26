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
    private static readonly TimeSpan BackgroundServiceStartTimeout = TimeSpan.FromSeconds(10);

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
        await processed.Task.WaitAsync(BackgroundServiceStartTimeout);
        stopping.Cancel();
        await service.StopAsync(CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<EnrichPendingBooksCommand>(command =>
                command.Isbn13s != null && command.Isbn13s.Count == 1 && command.Isbn13s[0] == isbn13),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenMultipleIsbnsAreQueued_SendsThemInOneEnrichmentRequest()
    {
        var queue = new BookMetadataEnrichmentQueue();
        var sender = Substitute.For<ISender>();
        var processed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        sender.Send(
                Arg.Any<EnrichPendingBooksCommand>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<EnrichPendingBooksCommand>();
                if (command.Isbn13s?.Count == 2)
                {
                    processed.TrySetResult(true);
                }

                return Task.FromResult(new EnrichPendingBooksResult(command.Isbn13s?.Count ?? 0, 0, 0, 0));
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
        await processed.Task.WaitAsync(BackgroundServiceStartTimeout);
        stopping.Cancel();
        await service.StopAsync(CancellationToken.None);

        await sender.Received(1).Send(
            Arg.Is<EnrichPendingBooksCommand>(command =>
                command.Isbn13s != null && command.Isbn13s.Count == 2 &&
                command.Isbn13s[0] == ParseIsbn("9791036377426") &&
                command.Isbn13s[1] == ParseIsbn("9783140464079")),
            Arg.Any<CancellationToken>());
    }

    private static Isbn13 ParseIsbn(string value)
    {
        return Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
    }
}
