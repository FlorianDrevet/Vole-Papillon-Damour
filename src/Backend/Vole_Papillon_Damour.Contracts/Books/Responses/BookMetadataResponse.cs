namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record BookMetadataResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    Uri? CoverUrl,
    string Source,
    string? WorkId,
    DateTimeOffset RetrievedAt);
