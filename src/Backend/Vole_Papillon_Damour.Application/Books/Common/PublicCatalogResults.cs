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
    string? CoverSource = null,
    string? RareBookSlug = null,
    Guid? RareBookId = null,
    string? Summary = null,
    IReadOnlyList<string>? Genres = null);

public sealed record PublicCatalogGenreFacetResult(string Name, int BookCount);

public sealed record PublicCatalogSearchResult(
    DateTime GeneratedAt,
    IReadOnlyList<PublicCatalogBookResult> Books,
    int? TotalCount,
    bool HasMore,
    int Page,
    int PageSize,
    IReadOnlyList<string> Genres,
    IReadOnlyList<PublicCatalogGenreFacetResult>? GenreFacets = null);

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
