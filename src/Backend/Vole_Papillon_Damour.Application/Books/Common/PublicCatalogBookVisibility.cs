using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public static class PublicCatalogBookVisibility
{
    public static readonly Expression<Func<Book, bool>> Predicate = book =>
        !book.IsHiddenFromCatalog &&
        !book.IsFavoriteReference &&
        book.RedirectedToIsbn13 == null &&
        (book.MetadataStatus == BookMetadataStatus.Resolved ||
         book.MetadataStatus == BookMetadataStatus.Manual) &&
        book.Title != null &&
        book.Title.Trim() != string.Empty;

    public static bool IsVisible(Book book)
    {
        return !book.IsHiddenFromCatalog &&
               !book.IsFavoriteReference &&
               book.RedirectedToIsbn13 is null &&
               book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual &&
               !string.IsNullOrWhiteSpace(book.Title);
    }

    public static bool IsVisible(PublicCatalogBookProjection book)
    {
        return !book.IsHiddenFromCatalog &&
               !book.IsFavoriteReference &&
               !book.HasRedirect &&
               book.MetadataStatus is BookMetadataStatus.Resolved or BookMetadataStatus.Manual &&
               !string.IsNullOrWhiteSpace(book.Title);
    }

    public static async Task<Isbn13[]> GetInProgressScanIsbnsAsync(
        IProjectDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var inProgressSessionIds = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.Status == ScanSessionStatus.InProgress)
            .Select(session => session.Id)
            .ToArrayAsync(cancellationToken);

        if (inProgressSessionIds.Length == 0)
        {
            return [];
        }

        return await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement => movement.ScanSessionId != null &&
                               inProgressSessionIds.Contains(movement.ScanSessionId))
            .Select(movement => movement.Isbn13)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Applies metadata visibility and in-progress scan-session filtering to public catalog books.
    /// </summary>
    public static IQueryable<Book> FilterPublicCatalogBooks(
        IQueryable<Book> books,
        IReadOnlyCollection<Isbn13> inProgressScanIsbns)
    {
        var visibleBooks = books.Where(Predicate);
        if (inProgressScanIsbns.Count == 0)
        {
            return visibleBooks;
        }

        // Scanning applies stock changes immediately; keep those ISBNs private while the session is open.
        return visibleBooks.Where(book => !inProgressScanIsbns.Contains(book.Id));
    }
}
