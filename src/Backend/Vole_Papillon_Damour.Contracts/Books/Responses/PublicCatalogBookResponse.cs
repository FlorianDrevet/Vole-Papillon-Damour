namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record PublicCatalogBookResponse(
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
