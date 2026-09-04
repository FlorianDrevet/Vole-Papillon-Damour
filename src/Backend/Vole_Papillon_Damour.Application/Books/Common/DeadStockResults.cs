namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record DeadStockBookResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? Genre,
    int QuantityAvailable,
    DateTime FirstAvailableAt);

public sealed record DeadStockResult(
    DateTime GeneratedAt,
    int MinAgeMonths,
    int MinQuantity,
    IReadOnlyList<DeadStockBookResult> Books);
