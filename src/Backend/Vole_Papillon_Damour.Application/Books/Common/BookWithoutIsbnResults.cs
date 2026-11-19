namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record BookWithoutIsbnItemResult(
    string Code,
    string? Title,
    string? Authors,
    string? Publisher,
    int QuantityAvailable,
    DateTime FirstSeenAt,
    IReadOnlyList<BookPhotoResult> Photos);

public sealed record BookWithoutIsbnPageResult(
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<BookWithoutIsbnItemResult> Items);