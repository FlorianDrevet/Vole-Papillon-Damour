using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record CloseIdleScanSessionsResult(int CandidateCount, int ClosedCount);

public sealed record ReleaseDueAnnouncementsResult(
    int ReleasedCount,
    int ReleasedQuantity,
    int DueUnreleasedCount = 0);

public sealed record AttachUndatedAnnouncementsToNextFairResult(
    AssoEventsId? TargetFairId,
    int AttachedCount,
    int DetachedCount = 0);

public sealed record EnrichPendingBooksResult(
    int ProcessedCount,
    int ResolvedCount,
    int NotFoundCount,
    int FailedCount,
    int CoverUpdatedCount = 0);
