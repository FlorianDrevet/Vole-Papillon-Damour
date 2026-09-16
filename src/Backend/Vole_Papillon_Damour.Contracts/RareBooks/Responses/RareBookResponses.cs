namespace Vole_Papillon_Damour.Contracts.RareBooks.Responses;

public sealed record RareBookPhotoResponse(
    Guid Id,
    Uri BlobUri,
    string BlobName,
    string? Caption,
    int Position,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    Guid UploadedBy);

public sealed record RareBookResponse(
    Guid Id,
    string Slug,
    string? Isbn13,
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    string Shelf,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Binding,
    string? Dimensions,
    int? PageCount,
    string? ShelfLocation,
    string Status,
    bool IsSold,
    DateTimeOffset? SoldAt,
    Guid? SoldAtFairId,
    Guid? SoldInSessionId,
    string? PriceSetBy,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    string RowVersion,
    IReadOnlyList<RareBookPhotoResponse> Photos);

public sealed record RareBookPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<RareBookResponse> Books,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RareBookPublishResponse(
    RareBookResponse RareBook,
    bool Changed,
    IReadOnlyList<string> Warnings);

public sealed record PublicRareBookResponse(
    Guid Id,
    string Slug,
    string? Isbn13,
    string Title,
    string? AuthorMention,
    string? Publisher,
    int? PublicationYear,
    string Shelf,
    decimal Price,
    string Condition,
    string? PublicDescription,
    string? Binding,
    string? Dimensions,
    int? PageCount,
    string Status,
    bool IsSold,
    DateTimeOffset? SoldAt,
    IReadOnlyList<RareBookPhotoResponse> Photos);

public sealed record PublicRareBookPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<PublicRareBookResponse> Books,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Shelves);

public sealed record PublicRareBookDetailResponse(
    PublicRareBookResponse RareBook,
    IReadOnlyList<PublicRareBookResponse> RelatedBooks);

public sealed record CashRareBookResponse(
    Guid Id,
    string Title,
    string? AuthorMention,
    string? Isbn13,
    decimal Price,
    string Shelf,
    string Condition,
    Uri? Thumbnail,
    string Slug);

public sealed record RareBookCashSearchResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<CashRareBookResponse> Books,
    int TotalCount,
    int Page,
    int PageSize);
