using System.Linq.Expressions;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class PublicCatalogBookVisibility
{
    public static readonly Expression<Func<Book, bool>> Predicate = book =>
        !book.IsHiddenFromCatalog &&
        book.RedirectedToIsbn13 == null &&
        (book.MetadataStatus == BookMetadataStatus.Resolved ||
         book.MetadataStatus == BookMetadataStatus.Manual) &&
        book.Title != null &&
        book.Title.Trim() != string.Empty;

    public static bool IsVisible(Book book)
    {
        return !book.IsHiddenFromCatalog &&
               book.RedirectedToIsbn13 is null &&
               book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual &&
               !string.IsNullOrWhiteSpace(book.Title);
    }

    /// <summary>
    /// Applies metadata visibility and in-progress scan-session filtering to public catalog books.
    /// </summary>
    public static IQueryable<Book> FilterPublicCatalogBooks(
        IQueryable<Book> books,
        IProjectDbContext dbContext)
    {
        return books
            .Where(Predicate)
            // Scanning applies stock changes immediately; keep those ISBNs private while the session is open.
            .Where(book => !dbContext.BookMovements.Any(movement =>
                movement.Isbn13 == book.Id &&
                movement.ScanSessionId != null &&
                dbContext.ScanSessions.Any(session =>
                    session.Id == movement.ScanSessionId &&
                    session.Status == ScanSessionStatus.InProgress)));
    }
}
