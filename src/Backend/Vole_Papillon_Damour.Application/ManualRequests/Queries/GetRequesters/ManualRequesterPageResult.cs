namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequesters;

public sealed record ManualRequesterPageResult(
    IReadOnlyList<ManualRequesterListItem> Requesters,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record ManualRequesterListItem(
    Guid RequesterId,
    string LastName,
    string FirstName,
    string Phone,
    int RequestCount,
    int SearchingCount,
    int FoundCount);
