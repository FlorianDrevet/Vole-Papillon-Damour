using FluentAssertions;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BookMetadataEnrichmentQueueTests
{
    [Fact]
    public async Task Enqueue_DeduplicatesAnIsbnUntilItIsDequeued()
    {
        var queue = new BookMetadataEnrichmentQueue();
        var isbn13 = ParseIsbn("9791036377426");

        queue.Enqueue(isbn13);
        queue.Enqueue(isbn13);

        var dequeued = await queue.DequeueAsync(CancellationToken.None);

        dequeued.Should().Be(isbn13);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Func<Task> dequeue = () => queue.DequeueAsync(cancelled.Token).AsTask();
        await dequeue.Should().ThrowAsync<OperationCanceledException>();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        return Isbn13.TryCreate(value, out var isbn)
            ? isbn
            : throw new InvalidOperationException($"Invalid test ISBN: {value}");
    }
}
