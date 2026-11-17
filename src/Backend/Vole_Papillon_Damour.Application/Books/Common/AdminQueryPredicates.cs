using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class AdminQueryPredicates
{
    public static IQueryable<Book> Catalogue(IQueryable<Book> books) =>
        books.Where(book =>
            (book.MetadataStatus == BookMetadataStatus.Resolved ||
             book.MetadataStatus == BookMetadataStatus.Manual) &&
            book.Title != null &&
            book.Title.Trim() != string.Empty);

    public static IQueryable<Book> MetadataQueue(IQueryable<Book> books) =>
        books.Where(book => book.MetadataStatus != BookMetadataStatus.Discarded)
            .Where(book =>
                book.MetadataStatus == BookMetadataStatus.Pending ||
                book.MetadataStatus == BookMetadataStatus.Failed ||
                book.MetadataStatus == BookMetadataStatus.NotFound ||
                book.Title == null ||
                book.Title.Trim() == string.Empty);

    public static IQueryable<Book> IsbndbCandidates(IQueryable<Book> books) =>
        books.Where(book =>
            !book.IsIsbndbSourced &&
            !book.IsAbsentFromIsbndb &&
            book.MetadataStatus != BookMetadataStatus.Discarded &&
            book.RedirectedToIsbn13 == null);

    public static IQueryable<Book> IsbndbAbsentToComplete(IQueryable<Book> books) =>
        books.Where(book =>
            book.IsAbsentFromIsbndb &&
            book.IsbndbCompletedAt == null &&
            book.Title != null &&
            book.Title.Trim() != string.Empty);
}
