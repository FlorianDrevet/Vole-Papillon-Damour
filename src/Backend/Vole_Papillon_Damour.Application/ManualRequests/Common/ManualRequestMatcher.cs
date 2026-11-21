using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

internal static class ManualRequestMatcher
{
    public static Task<ManualBookRequest?> FindOldestSearchingAsync(
        IProjectDbContext db,
        BookCode isbn13,
        string? workId,
        CancellationToken ct)
    {
        return db.ManualBookRequests
            .Where(request => request.Status == ManualBookRequestStatus.Searching &&
                              ((request.Scope == ManualBookRequestScope.Edition && request.Isbn13 == isbn13) ||
                               (request.Scope == ManualBookRequestScope.Work &&
                                workId != null && request.WorkId == workId)))
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .FirstOrDefaultAsync(ct);
    }

    public static Task<bool> AnySearchingWorkRequestAsync(IProjectDbContext db, CancellationToken ct)
    {
        return db.ManualBookRequests
            .AsNoTracking()
            .AnyAsync(request =>
                request.Status == ManualBookRequestStatus.Searching &&
                request.Scope == ManualBookRequestScope.Work,
                ct);
    }

    public static async Task<string?> ResolveWorkIdAsync(
        IProjectDbContext db,
        IBibliographicMetadataResolver? resolver,
        BookCode isbn13,
        CancellationToken ct)
    {
        var bookWorkId = await db.Books
            .AsNoTracking()
            .Where(book => book.Id == isbn13)
            .Select(book => book.WorkId)
            .SingleOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(bookWorkId))
        {
            return bookWorkId;
        }

        if (resolver is null)
        {
            return null;
        }

        // Les bibliothèques externes ne connaissent que les ISBN : un code interne n'est jamais résolu.
        return isbn13.TryGetIsbn13(out var isbn) ? await resolver.ResolveWorkIdAsync(isbn, ct) : null;
    }
}
