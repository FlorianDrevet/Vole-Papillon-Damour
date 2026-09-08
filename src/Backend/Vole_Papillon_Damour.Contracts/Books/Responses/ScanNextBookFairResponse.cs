namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanNextBookFairResponse(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset OpenAt,
    DateTimeOffset? CloseAt);
