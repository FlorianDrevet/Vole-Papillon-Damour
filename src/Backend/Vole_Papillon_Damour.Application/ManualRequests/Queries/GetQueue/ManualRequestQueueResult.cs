using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;

public sealed record ManualRequestQueueResult(
    IReadOnlyList<ManualRequestQueueItem> Items,
    int TotalCount,
    int FoundCount,
    int SearchingCount,
    int ClosedCount,
    int FoundOverdueCount,
    int Page,
    int PageSize);

public sealed record ManualRequestQueueItem(
    Guid RequestId,
    Guid RequesterId,
    string? RequesterLastName,
    string? RequesterFirstName,
    string? RequesterPhone,
    bool RequesterErased,
    ManualBookRequestScope Scope,
    string? WorkId,
    string? Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    DateTime RequestedAt,
    ManualBookRequestStatus Status,
    ManualBookRequestFoundOrigin? FoundOrigin,
    string? FoundIsbn13,
    string? FoundPublisher,
    int? FoundPublicationYear,
    DateTime? FoundAt,
    string? FoundByName,
    int QuantityAvailable,
    int QuantityAnnounced,
    int WatcherCount,
    ManualRequestReleaseMode? ReleaseMode,
    DateTimeOffset? ReleaseFairStart,
    DateTime? ClosedAt,
    string? ClosedByName);
