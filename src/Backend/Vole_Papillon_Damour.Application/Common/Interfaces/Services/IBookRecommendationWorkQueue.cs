namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookRecommendationWorkQueue
{
    Task EnqueueBooksAsync(
        IReadOnlyCollection<BookRecommendationQueueItem> books,
        CancellationToken cancellationToken);

    Task EnqueueBackfillPageAsync(
        Guid backfillId,
        string? cursor,
        CancellationToken cancellationToken);
}
