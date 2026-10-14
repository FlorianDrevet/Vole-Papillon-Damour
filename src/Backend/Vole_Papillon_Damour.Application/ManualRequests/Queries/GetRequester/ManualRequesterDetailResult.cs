using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequester;

public sealed record ManualRequesterDetailResult(
    Guid RequesterId,
    string? LastName,
    string? FirstName,
    string? Phone,
    bool Erased,
    DateTime ConsentRecordedAt,
    string? ConsentRecordedByName,
    IReadOnlyList<ManualRequestQueueItem> Requests);
