namespace Vole_Papillon_Damour.Contracts.ManualRequests;

public sealed record ManualRequestQueueResponse(
    IReadOnlyList<ManualRequestQueueItemResponse> Items,
    int TotalCount,
    int FoundCount,
    int SearchingCount,
    int ClosedCount,
    int FoundOverdueCount,
    int Page,
    int PageSize);

public sealed record ManualRequestQueueItemResponse(
    Guid RequestId,
    Guid RequesterId,
    string? RequesterLastName,
    string? RequesterFirstName,
    string? RequesterPhone,
    bool RequesterErased,
    string Scope,
    string? WorkId,
    string? Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    DateTime RequestedAt,
    string Status,
    string? FoundOrigin,
    string? FoundIsbn13,
    string? FoundPublisher,
    int? FoundPublicationYear,
    DateTime? FoundAt,
    string? FoundByName,
    int QuantityAvailable,
    int QuantityAnnounced,
    int WatcherCount,
    string? ReleaseMode,
    DateTimeOffset? ReleaseFairStart,
    DateTime? ClosedAt,
    string? ClosedByName);

public sealed record ManualRequestSummaryResponse(int FoundCount, int FoundOverdueCount);

public sealed record ManualRequestStockResponse(
    int QuantityAvailable,
    int QuantityAnnounced,
    DateTimeOffset? NextFairStart);

public sealed record ManualRequesterDetailResponse(
    Guid RequesterId,
    string? LastName,
    string? FirstName,
    string? Phone,
    bool Erased,
    DateTime ConsentRecordedAt,
    string? ConsentRecordedByName,
    IReadOnlyList<ManualRequestQueueItemResponse> Requests);

public sealed record ManualRequesterCreatedResponse(Guid RequesterId);

public sealed record ManualBookRequestCreatedResponse(Guid RequestId, string Status);

public sealed record ManualRequestReleaseResponse(int NotifiedMemberCount);

public sealed record ManualRequesterDeletionResponse(int CancelledRequestCount, int NotifiedMemberCount);
