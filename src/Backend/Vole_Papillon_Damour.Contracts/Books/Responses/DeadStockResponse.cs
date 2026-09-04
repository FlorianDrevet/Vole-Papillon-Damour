namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record DeadStockResponse(
    DateTimeOffset GeneratedAt,
    int MinAgeMonths,
    int MinQuantity,
    IReadOnlyList<DeadStockBookResponse> Books);
