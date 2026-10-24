namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record IsbndbEditionSearchResponse(
    IReadOnlyList<IsbndbEditionSearchItemResponse> Items,
    int Total,
    bool Truncated,
    string? Unavailable);

public sealed record IsbndbEditionSearchItemResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? CoverUrl,
    string? Binding);
