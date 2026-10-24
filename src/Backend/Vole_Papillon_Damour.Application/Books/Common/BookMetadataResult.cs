using Vole_Papillon_Damour.Application.Isbndb;

namespace Vole_Papillon_Damour.Application.Books.Common;

public enum IsbndbOutcome : byte
{
    Found,
    NotFound,
    Deferred
}

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
    string? CoverSource = null,
    string? Genre = null,
    IReadOnlyList<string>? Genres = null,
    string? Summary = null,
    string? PhysicalFormat = null,
    string? Language = null,
    IsbndbOutcome? IsbndbOutcome = null,
    IsbndbBook? IsbndbNotice = null,
    int? Pages = null,
    string? Edition = null,
    string? WorkKey = null);
