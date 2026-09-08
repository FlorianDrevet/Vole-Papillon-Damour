namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanNextBookFairResult(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset OpenAt,
    DateTimeOffset? CloseAt);
