using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

public sealed record ManualRequestStockSnapshot(
    int QuantityAvailable,
    int QuantityAnnounced,
    DateTimeOffset? NextFairStart);

internal static class ManualRequestStock
{
    public static async Task<ManualRequestStockSnapshot> GetAsync(
        IProjectDbContext db,
        ManualBookRequestScope scope,
        string? workId,
        Isbn13? isbn13,
        CancellationToken ct)
    {
        var booksQuery = db.Books
            .AsNoTracking()
            .Where(book => book.RedirectedToIsbn13 == null);

        if (scope == ManualBookRequestScope.Work)
        {
            if (string.IsNullOrWhiteSpace(workId))
            {
                return new ManualRequestStockSnapshot(0, 0, null);
            }

            booksQuery = booksQuery.Where(book => book.WorkId == workId);
        }
        else if (scope == ManualBookRequestScope.Edition)
        {
            if (isbn13 is null)
            {
                return new ManualRequestStockSnapshot(0, 0, null);
            }

            booksQuery = booksQuery.Where(book => book.Id == isbn13);
        }
        else
        {
            return new ManualRequestStockSnapshot(0, 0, null);
        }

        var books = await booksQuery
            .Select(book => new { book.Id, book.QuantityAvailable })
            .ToListAsync(ct);
        var quantityAvailable = books.Sum(book => book.QuantityAvailable);
        var isbn13s = books.Select(book => book.Id).ToArray();

        // An edition request still has a target ISBN even if the book has never
        // been received and therefore has no row in Books yet.
        if (scope == ManualBookRequestScope.Edition && isbn13 is { } editionIsbn && isbn13s.Length == 0)
        {
            isbn13s = [editionIsbn];
        }

        if (isbn13s.Length == 0)
        {
            return new ManualRequestStockSnapshot(quantityAvailable, 0, null);
        }

        var announcements = await db.BookAnnouncements
            .AsNoTracking()
            .Where(announcement =>
                isbn13s.Contains(announcement.Isbn13) &&
                announcement.Status == BookAnnouncementStatus.Announced &&
                announcement.AssoEventsId != null)
            .ToListAsync(ct);
        var quantityAnnounced = announcements.Sum(announcement => announcement.Quantity);
        if (quantityAnnounced == 0)
        {
            return new ManualRequestStockSnapshot(quantityAvailable, 0, null);
        }

        var fairIds = announcements
            .Where(announcement => announcement.AssoEventsId is not null)
            .Select(announcement => announcement.AssoEventsId!)
            .Distinct()
            .ToArray();
        var fairOpenings = await db.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .Where(assoEvent => fairIds.Contains(assoEvent.Id))
            .Select(assoEvent => new { assoEvent.DateStart, assoEvent.HourOpenDoors })
            .ToListAsync(ct);
        var nextFairStart = fairOpenings
            .Select(fair => fair.HourOpenDoors ?? fair.DateStart)
            .OrderBy(start => start)
            .Select(start => (DateTimeOffset?)start)
            .FirstOrDefault();

        return new ManualRequestStockSnapshot(quantityAvailable, quantityAnnounced, nextFairStart);
    }
}
