namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record BookMetadataResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    Uri? CoverUrl,
    string Source,
    string? WorkId,
    DateTimeOffset RetrievedAt,
    string? CoverSource = null);
