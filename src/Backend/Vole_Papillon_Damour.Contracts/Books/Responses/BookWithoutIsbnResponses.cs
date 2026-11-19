using Vole_Papillon_Damour.Contracts.Books.Requests;

namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record BookWithoutIsbnItemResponse(
    string Code,
    string? Title,
    string? Authors,
    string? Publisher,
    int QuantityAvailable,
    DateTime FirstSeenAt,
    IReadOnlyList<BookPhotoResponse> Photos);

public sealed record BookWithoutIsbnPageResponse(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<BookWithoutIsbnItemResponse> Items);