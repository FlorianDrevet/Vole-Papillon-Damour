using MediatR;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.SeedRecommendationsBackfillPage;

public sealed record SeedRecommendationsBackfillPageCommand(Guid BackfillId, string? Cursor)
    : IRequest<SeedRecommendationsBackfillPageResult>;

public sealed record SeedRecommendationsBackfillPageResult(
    int BooksScanned,
    int BooksQueued,
    string? NextCursor,
    bool IsComplete);
