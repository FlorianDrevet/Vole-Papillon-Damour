using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
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

    /// <summary>
    /// Batch variant of <see cref="GetAsync"/> for the searching requests of one page. Returns the same
    /// snapshot as <see cref="GetAsync"/> for each request, with one query per table instead of one per request.
    /// Work ids are matched case-insensitively, like the database collation used by <see cref="GetAsync"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ManualRequestStockSnapshot>> GetManyAsync(
        IProjectDbContext db,
        IReadOnlyCollection<ManualBookRequest> requests,
        CancellationToken ct)
    {
        var workIds = requests
            .Where(request => request.Scope == ManualBookRequestScope.Work && !string.IsNullOrWhiteSpace(request.WorkId))
            .Select(request => request.WorkId!)
            .Distinct()
            .ToArray();
        var editionIsbns = requests
            .Where(request => request.Scope == ManualBookRequestScope.Edition && request.Isbn13 is not null)
            .Select(request => request.Isbn13!.Value)
            .Distinct()
            .ToArray();
        var books = workIds.Length == 0 && editionIsbns.Length == 0
            ? new List<BookStockRow>()
            : await db.Books
                .AsNoTracking()
                .Where(book => book.RedirectedToIsbn13 == null &&
                               ((workIds.Length > 0 && workIds.Contains(book.WorkId)) || editionIsbns.Contains(book.Id)))
                .Select(book => new BookStockRow(book.Id, book.WorkId, book.QuantityAvailable))
                .ToListAsync(ct);
        var booksByWork = books
            .Where(book => book.WorkId is not null)
            .ToLookup(book => book.WorkId!, StringComparer.OrdinalIgnoreCase);
        var booksByIsbn = books.ToLookup(book => book.Isbn13);

        // Per request: its available quantity and the ISBNs whose announcements count toward it.
        var scopes = new Dictionary<Guid, (int QuantityAvailable, Isbn13[] Isbns)>(requests.Count);
        foreach (var request in requests)
        {
            if (request.Scope == ManualBookRequestScope.Work && !string.IsNullOrWhiteSpace(request.WorkId))
            {
                var workBooks = booksByWork[request.WorkId!].ToArray();
                scopes[request.Id] = (workBooks.Sum(book => book.QuantityAvailable), workBooks.Select(book => book.Isbn13).ToArray());
            }
            else if (request.Scope == ManualBookRequestScope.Edition && request.Isbn13 is { } editionIsbn)
            {
                // An edition request still targets its ISBN when the book has no row yet (see GetAsync).
                var editionBooks = booksByIsbn[editionIsbn].ToArray();
                scopes[request.Id] = (editionBooks.Sum(book => book.QuantityAvailable), [editionIsbn]);
            }
            else
            {
                scopes[request.Id] = (0, []);
            }
        }

        var allIsbns = scopes.Values.SelectMany(scope => scope.Isbns).Distinct().ToArray();
        var announcements = allIsbns.Length == 0
            ? new List<AnnouncementStockRow>()
            : await db.BookAnnouncements
                .AsNoTracking()
                .Where(announcement =>
                    allIsbns.Contains(announcement.Isbn13) &&
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    announcement.AssoEventsId != null)
                .Select(announcement => new AnnouncementStockRow(
                    announcement.Isbn13,
                    announcement.Quantity,
                    announcement.AssoEventsId))
                .ToListAsync(ct);

        var fairIds = announcements
            .Where(announcement => announcement.AssoEventsId is not null)
            .Select(announcement => announcement.AssoEventsId!)
            .Distinct()
            .ToArray();
        var fairOpenings = fairIds.Length == 0
            ? new List<FairOpeningRow>()
            : await db.AssoEvents
                .AsNoTracking()
                .WhereActiveBookFair()
                .Where(assoEvent => fairIds.Contains(assoEvent.Id))
                .Select(assoEvent => new FairOpeningRow(assoEvent.Id, assoEvent.DateStart, assoEvent.HourOpenDoors))
                .ToListAsync(ct);
        var fairStarts = fairOpenings.ToDictionary(
            fair => fair.FairId,
            fair => fair.HourOpenDoors ?? fair.DateStart);

        var snapshots = new Dictionary<Guid, ManualRequestStockSnapshot>(requests.Count);
        foreach (var request in requests)
        {
            var (quantityAvailable, isbns) = scopes[request.Id];
            var matched = announcements.Where(announcement => isbns.Contains(announcement.Isbn13)).ToArray();
            var quantityAnnounced = matched.Sum(announcement => announcement.Quantity);
            DateTimeOffset? nextFairStart = null;
            if (quantityAnnounced > 0)
            {
                var starts = matched
                    .Where(announcement => announcement.AssoEventsId is not null &&
                                           fairStarts.ContainsKey(announcement.AssoEventsId))
                    .Select(announcement => fairStarts[announcement.AssoEventsId!])
                    .ToArray();
                nextFairStart = starts.Length == 0 ? null : starts.Min();
            }

            snapshots[request.Id] = new ManualRequestStockSnapshot(quantityAvailable, quantityAnnounced, nextFairStart);
        }

        return snapshots;
    }

    private sealed record BookStockRow(Isbn13 Isbn13, string? WorkId, int QuantityAvailable);

    private sealed record AnnouncementStockRow(Isbn13 Isbn13, int Quantity, AssoEventsId? AssoEventsId);

    private sealed record FairOpeningRow(AssoEventsId FairId, DateTimeOffset DateStart, DateTimeOffset? HourOpenDoors);
}
