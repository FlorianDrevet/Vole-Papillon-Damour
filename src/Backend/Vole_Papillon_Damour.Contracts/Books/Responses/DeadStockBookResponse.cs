namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record DeadStockBookResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? Genre,
    int QuantityAvailable,
    DateTimeOffset FirstAvailableAt);
