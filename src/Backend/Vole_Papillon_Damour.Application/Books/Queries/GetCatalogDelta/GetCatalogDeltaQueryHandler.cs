using System.Globalization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Queries.GetCatalogDelta;

public sealed class GetCatalogDeltaQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetCatalogDeltaQuery, ErrorOr<ScanCatalogDeltaResult>>
{
    private static readonly DateTime DefaultSettingsUpdatedAt =
        new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<ErrorOr<ScanCatalogDeltaResult>> Handle(
        GetCatalogDeltaQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryParseWatermark(query.Since, out var since))
        {
            return Error.Validation(
                "Books.InvalidCatalogWatermark",
                "The catalog watermark is invalid.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var upcomingBookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .ToListAsync(cancellationToken);
        var nextFair = upcomingBookFairs
            .Where(assoEvent =>
                (assoEvent.DateEnd ?? assoEvent.DateStart) > new DateTimeOffset(generatedAt, TimeSpan.Zero))
            .OrderBy(assoEvent => assoEvent.DateStart)
            .ThenBy(assoEvent => assoEvent.Id.Value)
            .FirstOrDefault();

        var selectedBooks = await GetBooksToProjectAsync(
            since,
            generatedAt,
            cancellationToken);
        var wantedTargets = await GetWantedTargetsAsync(cancellationToken);
        var announcedQuantities = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.Status == BookAnnouncementStatus.Announced)
            .GroupBy(announcement => announcement.Isbn13)
            .Select(group => new
            {
                Isbn13 = group.Key,
                Quantity = group.Sum(announcement => announcement.Quantity)
            })
            .ToDictionaryAsync(row => row.Isbn13, row => row.Quantity, cancellationToken);
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);
        settings ??= AssociationSettingsEntity.Create(
            Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(Guid.Empty),
            DefaultSettingsUpdatedAt);

        var books = selectedBooks.Books
            .OrderBy(book => book.Isbn13.Value, StringComparer.Ordinal)
            .Select(book => new ScanCatalogBookResult(
                book.Isbn13.Value,
                book.Title,
                book.Authors,
                book.WorkId,
                book.QuantityAvailable,
                announcedQuantities.GetValueOrDefault(book.Isbn13),
                book.SalesCount,
                wantedTargets.Matches(book),
                false,
                book.IsHiddenFromCatalog,
                book.UpdatedAt))
            .ToArray();

        var rareBooks = await GetRareBooksToProjectAsync(
            since,
            generatedAt,
            cancellationToken);
        // The rare-book delta contains changed fiches and tombstones only. The
        // ordinary projection still needs the current membership for every book
        // returned in this response, otherwise an unchanged rare fiche would
        // incorrectly clear IsRare on an incremental sync.
        var currentRareIsbns = await dbContext.RareBooks
            .AsNoTracking()
            .PublishedAvailable()
            .Select(book => book.Isbn13!.Value.Value)
            .ToHashSetAsync(cancellationToken);
        books = books
            .Select(book => book with { IsRare = currentRareIsbns.Contains(book.Isbn13) })
            .ToArray();

        return new ScanCatalogDeltaResult(
            generatedAt,
            SerializeWatermark(selectedBooks.RowVersion, generatedAt),
            books,
            rareBooks,
            AssociationSettingsResult.From(settings),
            nextFair is null
                ? null
                : new ScanNextBookFairResult(
                    nextFair.Id.Value,
                    nextFair.Name,
                    nextFair.DateStart,
                    nextFair.DateEnd,
                    BookFairSchedule.GetOpeningInstant(nextFair),
                    BookFairSchedule.GetClosingInstant(nextFair)));
    }

    private async Task<IReadOnlyList<ScanCatalogRareBookResult>> GetRareBooksToProjectAsync(
        CatalogWatermark? since,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var rareBooksQuery = dbContext.RareBooks
            .AsNoTracking()
            .Include(book => book.Photos)
            .Where(book =>
                book.Isbn13 != null &&
                book.UpdatedAt <= generatedAt);
        if (since is null)
        {
            rareBooksQuery = rareBooksQuery.Where(book =>
                book.Status == RareBookStatus.Published && !book.IsSold);
        }
        else
        {
            rareBooksQuery = rareBooksQuery.Where(book => book.UpdatedAt > since.AsOf);
        }

        var rareBooks = await rareBooksQuery
            .OrderBy(book => book.UpdatedAt)
            .ThenBy(book => book.Id)
            .ToListAsync(cancellationToken);

        return rareBooks
            .Select(book => new ScanCatalogRareBookResult(
                book.Id.Value,
                book.Isbn13!.Value.Value,
                book.Title,
                book.AuthorMention,
                book.Price,
                book.Shelf.Value,
                book.Condition.Value.ToString(),
                book.PublicDescription,
                book.Photos
                    .OrderBy(photo => photo.Position)
                    .Select(photo => (Uri?)photo.BlobUri)
                    .FirstOrDefault(),
                book.Status == RareBookStatus.Published && !book.IsSold,
                book.UpdatedAt))
            .ToArray();
    }

    private async Task<CatalogBookSelection> GetBooksToProjectAsync(
        CatalogWatermark? since,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        // On SQL Server every row below MIN_ACTIVE_ROWVERSION() is committed. Bounding
        // the read there, and serving that bound as the next watermark, keeps a write
        // still in flight with a lower rowversion from being skipped by every device.
        var committedUpperBound = await RowVersionQueries.GetMinActiveRowVersionAsync(
            dbContext.Database,
            cancellationToken);
        var catalog = dbContext.Books
            .AsNoTracking()
            .Where(book => book.UpdatedAt <= generatedAt);
        if (committedUpperBound is not null)
        {
            catalog = catalog.Where(book =>
                RowVersionQueries.IsGreaterThan(committedUpperBound, book.RowVersion));
        }

        var changedBooks = await SelectRows(
                FilterChangedBooks(catalog, since, committedUpperBound is not null))
            .ToListAsync(cancellationToken);
        var rowVersion = committedUpperBound is not null
            ? Latest(DecrementRowVersion(committedUpperBound), since?.RowVersion ?? [])
            : HighestRowVersion(changedBooks, since?.RowVersion);

        if (since is null)
        {
            return new CatalogBookSelection(
                changedBooks.Where(book => !book.IsHiddenFromCatalog).ToArray(),
                rowVersion);
        }

        var recentWatchlistItems = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.AddedAt > since.AsOf && item.AddedAt <= generatedAt)
            .Select(item => new WatchlistTarget(item.Scope, item.Isbn13, item.WorkId))
            .ToListAsync(cancellationToken);
        var watchlistStateChanged = await dbContext.Watchlists
            .AsNoTracking()
            .AnyAsync(
                watchlist => watchlist.UpdatedAt > since.AsOf && watchlist.UpdatedAt <= generatedAt,
                cancellationToken);
        if (recentWatchlistItems.Count == 0 && !watchlistStateChanged)
        {
            return new CatalogBookSelection(changedBooks, rowVersion);
        }

        var visibleCatalog = catalog.Where(book => !book.IsHiddenFromCatalog);
        if (!watchlistStateChanged)
        {
            // Only the fiches targeted by the newly added items can change their flag.
            // A suspended, reactivated or removed item cannot be traced back to its
            // fiches, so that case still re-projects the visible catalog.
            var targets = WantedTargets.From(recentWatchlistItems);
            var isbn13s = targets.Isbn13s.ToArray();
            var workIds = targets.WorkIds.ToArray();
            visibleCatalog = visibleCatalog.Where(book =>
                isbn13s.Contains(book.Id) ||
                (book.WorkId != null && workIds.Contains(book.WorkId)));
        }

        var reprojectedBooks = await SelectRows(visibleCatalog).ToListAsync(cancellationToken);
        var selected = changedBooks
            .Concat(reprojectedBooks)
            .GroupBy(book => book.Isbn13.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new CatalogBookSelection(selected, rowVersion);
    }

    private static IQueryable<Book> FilterChangedBooks(
        IQueryable<Book> catalog,
        CatalogWatermark? since,
        bool rowVersionsAreServerAssigned)
    {
        if (since is null)
        {
            return catalog;
        }

        var sinceAsOf = since.AsOf;
        if (since.RowVersion.Length == 0)
        {
            return catalog.Where(book => book.UpdatedAt > sinceAsOf);
        }

        var sinceRowVersion = since.RowVersion;
        if (rowVersionsAreServerAssigned)
        {
            // Kept as a single range predicate so SQL Server can seek IX_Books_RowVersion.
            return catalog.Where(book => RowVersionQueries.IsGreaterThan(book.RowVersion, sinceRowVersion));
        }

        // Providers without server-assigned rowversions can hold empty values, so the
        // business timestamp is also accepted. Re-sending an unchanged fiche is harmless:
        // devices deduplicate the delta by ISBN.
        return catalog.Where(book =>
            RowVersionQueries.IsGreaterThan(book.RowVersion, sinceRowVersion) ||
            book.UpdatedAt > sinceAsOf);
    }

    private static IQueryable<CatalogBookRow> SelectRows(IQueryable<Book> books)
    {
        return books.Select(book => new CatalogBookRow(
            book.Id,
            book.Title,
            book.Authors,
            book.WorkId,
            book.QuantityAvailable,
            book.SalesCount,
            book.IsHiddenFromCatalog,
            book.UpdatedAt,
            book.RowVersion));
    }

    private async Task<WantedTargets> GetWantedTargetsAsync(CancellationToken cancellationToken)
    {
        var activeWatchlistIds = await dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.AlertStatus == WatchlistAlertStatus.Active)
            .Select(watchlist => watchlist.Id)
            .ToListAsync(cancellationToken);
        if (activeWatchlistIds.Count == 0)
        {
            return WantedTargets.From([]);
        }

        var items = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => activeWatchlistIds.Contains(item.UserId))
            .Select(item => new WatchlistTarget(item.Scope, item.Isbn13, item.WorkId))
            .ToListAsync(cancellationToken);
        return WantedTargets.From(items);
    }

    private static bool TryParseWatermark(
        string? value,
        out CatalogWatermark? watermark)
    {
        watermark = null;
        if (value is null or "")
        {
            return true;
        }

        if (value.StartsWith("v2.", StringComparison.Ordinal))
        {
            var parts = value.Split('.', StringSplitOptions.None);
            if (parts.Length != 3 ||
                !TryDecodeRowVersion(parts[1], out var rowVersion) ||
                !long.TryParse(
                    parts[2],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var ticks) ||
                ticks < DateTime.MinValue.Ticks ||
                ticks > DateTime.MaxValue.Ticks)
            {
                return false;
            }

            watermark = new CatalogWatermark(
                rowVersion,
                new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var legacyTimestamp))
        {
            return false;
        }

        watermark = new CatalogWatermark([], legacyTimestamp.UtcDateTime);
        return true;
    }

    private static bool TryDecodeRowVersion(
        string encoded,
        out byte[] rowVersion)
    {
        rowVersion = [];
        if (encoded.Length == 0)
        {
            return true;
        }

        var base64 = encoded.Replace('-', '+').Replace('_', '/');
        base64 = (base64.Length % 4) switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => string.Empty,
        };
        if (base64.Length == 0)
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(base64);
            return rowVersion.Length is 0 or 8;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string SerializeWatermark(
        byte[] rowVersion,
        DateTime generatedAt)
    {
        var encodedRowVersion = Convert.ToBase64String(rowVersion)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"v2.{encodedRowVersion}.{generatedAt.Ticks.ToString(CultureInfo.InvariantCulture)}";
    }

    private static byte[] HighestRowVersion(
        IEnumerable<CatalogBookRow> books,
        byte[]? previousRowVersion)
    {
        var highest = previousRowVersion ?? [];
        foreach (var book in books)
        {
            highest = Latest(book.RowVersion, highest);
        }

        return highest.ToArray();
    }

    private static byte[] DecrementRowVersion(byte[] rowVersion)
    {
        var result = rowVersion.ToArray();
        for (var index = result.Length - 1; index >= 0; index--)
        {
            if (result[index] > 0)
            {
                result[index]--;
                return result;
            }

            result[index] = byte.MaxValue;
        }

        // An all-zero value has no predecessor.
        return rowVersion.ToArray();
    }

    private static byte[] Latest(byte[] left, byte[] right)
    {
        return CompareRowVersions(left, right) >= 0 ? left : right;
    }

    private static int CompareRowVersions(byte[] left, byte[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private sealed record CatalogWatermark(byte[] RowVersion, DateTime AsOf);

    private sealed record CatalogBookSelection(
        IReadOnlyList<CatalogBookRow> Books,
        byte[] RowVersion);

    private sealed record CatalogBookRow(
        Isbn13 Isbn13,
        string? Title,
        string? Authors,
        string? WorkId,
        int QuantityAvailable,
        int SalesCount,
        bool IsHiddenFromCatalog,
        DateTime UpdatedAt,
        byte[] RowVersion);

    private sealed record WatchlistTarget(
        WatchlistItemScope Scope,
        Isbn13? Isbn13,
        string? WorkId);

    private sealed class WantedTargets
    {
        private readonly HashSet<Isbn13> isbn13s;
        private readonly HashSet<string> workIds;

        private WantedTargets(HashSet<Isbn13> isbn13s, HashSet<string> workIds)
        {
            this.isbn13s = isbn13s;
            this.workIds = workIds;
        }

        public IReadOnlyCollection<Isbn13> Isbn13s => isbn13s;

        public IReadOnlyCollection<string> WorkIds => workIds;

        public static WantedTargets From(IReadOnlyCollection<WatchlistTarget> items)
        {
            return new WantedTargets(
                items
                    .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 is not null)
                    .Select(item => item.Isbn13!.Value)
                    .ToHashSet(),
                items
                    .Where(item => item.Scope == WatchlistItemScope.Work && item.WorkId is not null)
                    .Select(item => item.WorkId!)
                    .ToHashSet(StringComparer.Ordinal));
        }

        public bool Matches(CatalogBookRow book)
        {
            return isbn13s.Contains(book.Isbn13) ||
                   (book.WorkId is not null && workIds.Contains(book.WorkId));
        }
    }
}
