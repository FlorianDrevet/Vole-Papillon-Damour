namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record PublicBookFairResponse(
    Guid Id,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    DateTimeOffset OpenAt,
    DateTimeOffset? CloseAt,
    int? RoadNumber,
    string City,
    int CityCode,
    string Road);
