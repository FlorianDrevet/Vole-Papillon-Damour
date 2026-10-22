namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public enum BookRecommendationQueueMessageKind : byte
{
    ProcessBooks = 0,
    BackfillPage = 1,
}

public sealed record BookRecommendationQueueItem(string Isbn13, int RequestVersion);

public sealed record BookRecommendationQueueMessage(
    BookRecommendationQueueMessageKind Kind,
    IReadOnlyList<BookRecommendationQueueItem>? Books = null,
    Guid? BackfillId = null,
    string? Cursor = null);
