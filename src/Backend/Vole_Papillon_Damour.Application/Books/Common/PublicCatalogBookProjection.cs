using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record PublicCatalogBookProjection(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? WorkId,
    string? CoverUrl,
    int QuantityAvailable,
    DateTime? LastAvailableAt,
    DateTime FirstSeenAt,
    DateTime UpdatedAt,
    BookMetadataStatus MetadataStatus,
    bool IsHiddenFromCatalog,
    bool IsFavoriteReference,
    bool HasRedirect,
    BookCoverSource? CoverSource,
    IReadOnlyList<string> Genres)
{
    public static PublicCatalogBookProjection FromBook(Book book)
    {
        return new PublicCatalogBookProjection(
            book.Id.Value,
            book.Title,
            book.Authors,
            book.Publisher,
            book.PublicationYear,
            book.PhysicalFormat,
            book.Language,
            book.Genre,
            book.WorkId,
            book.CoverUrl,
            book.QuantityAvailable,
            book.LastAvailableAt,
            book.FirstSeenAt,
            book.UpdatedAt,
            book.MetadataStatus,
            book.IsHiddenFromCatalog,
            book.IsFavoriteReference,
            book.RedirectedToIsbn13 is not null,
            book.CoverSource,
            book.Genres.Select(genre => genre.Name).ToArray());
    }
}
