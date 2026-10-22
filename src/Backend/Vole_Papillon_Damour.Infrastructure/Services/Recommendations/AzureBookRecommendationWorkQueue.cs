using System.Text.Json;
using Azure.Storage.Queues;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Recommendations;

public sealed class AzureBookRecommendationWorkQueue(QueueClient queueClient) : IBookRecommendationWorkQueue
{
    private const int BooksPerMessage = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueBooksAsync(
        IReadOnlyCollection<BookRecommendationQueueItem> books,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(books);
        if (books.Count == 0)
        {
            return;
        }

        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var orderedBooks = books
            .DistinctBy(book => (book.Isbn13, book.RequestVersion))
            .ToArray();
        for (var offset = 0; offset < orderedBooks.Length; offset += BooksPerMessage)
        {
            var batch = orderedBooks.Skip(offset).Take(BooksPerMessage).ToArray();
            var message = new BookRecommendationQueueMessage(
                BookRecommendationQueueMessageKind.ProcessBooks,
                Books: batch);
            await queueClient.SendMessageAsync(
                JsonSerializer.Serialize(message, JsonOptions),
                cancellationToken);
        }
    }

    public async Task EnqueueBackfillPageAsync(
        Guid backfillId,
        string? cursor,
        CancellationToken cancellationToken)
    {
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var message = new BookRecommendationQueueMessage(
            BookRecommendationQueueMessageKind.BackfillPage,
            BackfillId: backfillId,
            Cursor: cursor);
        await queueClient.SendMessageAsync(
            JsonSerializer.Serialize(message, JsonOptions),
            cancellationToken);
    }
}
