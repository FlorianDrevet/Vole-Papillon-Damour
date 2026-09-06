namespace Vole_Papillon_Damour.Application.Books.Common;

public enum PublicCatalogAvailabilityFilter
{
    All,
    AvailableNow,
    NextBookFair
}

public enum PublicCatalogSortOrder
{
    Relevance,
    RecentlyAdded
}

public sealed record PublicCatalogBookResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? WorkId,
    string? CoverUrl,
    int QuantityAvailable,
    int QuantityAnnounced,
    DateTimeOffset? NextFairAt,
    DateTimeOffset? LastAvailableAt,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset UpdatedAt,
    bool IsRare,
    string? CoverSource = null);

public sealed record PublicCatalogSearchResult(
    DateTime GeneratedAt,
    IReadOnlyList<PublicCatalogBookResult> Books,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Genres);

public sealed record PublicBookFairResult(
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

public sealed record PublicCatalogWorkResult(
    string WorkId,
    string? Title,
    string? Authors,
    IReadOnlyList<PublicCatalogBookResult> Editions);

public sealed record PublicCatalogSitemapEntry(
    string UrlPath,
    DateTimeOffset LastModified);

public sealed record PublicCatalogSitemapResult(
    IReadOnlyList<PublicCatalogSitemapEntry> Entries);
