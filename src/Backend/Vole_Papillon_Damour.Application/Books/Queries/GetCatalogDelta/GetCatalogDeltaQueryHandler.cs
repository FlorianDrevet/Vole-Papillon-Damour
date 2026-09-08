using System.Globalization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
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
            .Where(assoEvent => !assoEvent.IsCancelled)
            .ToListAsync(cancellationToken);
        var nextFair = upcomingBookFairs
            .Where(assoEvent =>
                assoEvent.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                (assoEvent.DateEnd ?? assoEvent.DateStart) > new DateTimeOffset(generatedAt, TimeSpan.Zero))
            .OrderBy(assoEvent => assoEvent.DateStart)
            .ThenBy(assoEvent => assoEvent.Id.Value)
            .FirstOrDefault();

        var selectedBooks = await GetBooksToProjectAsync(
            since,
            generatedAt,
            cancellationToken);
        var activeWatchlistIds = await dbContext.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.AlertStatus == WatchlistAlertStatus.Active)
            .Select(watchlist => watchlist.Id)
            .ToListAsync(cancellationToken);
        var activeWatchlistItems = activeWatchlistIds.Count == 0
            ? []
            : await dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => activeWatchlistIds.Contains(item.UserId))
                .ToListAsync(cancellationToken);
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.Status == BookAnnouncementStatus.Announced)
            .ToListAsync(cancellationToken);
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);
        settings ??= AssociationSettingsEntity.Create(
            Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(Guid.Empty),
            DefaultSettingsUpdatedAt);

        var books = selectedBooks.Books
            .OrderBy(book => book.Id.Value, StringComparer.Ordinal)
            .Select(book => new ScanCatalogBookResult(
                book.Id.Value,
                book.Title,
                book.Authors,
                book.WorkId,
                book.QuantityAvailable,
                announcements
                    .Where(announcement => announcement.Isbn13 == book.Id)
                    .Sum(announcement => announcement.Quantity),
                book.SalesCount,
                activeWatchlistItems.Any(item => Matches(item, book)),
                book.IsRare,
                book.IsHiddenFromCatalog,
                book.UpdatedAt))
            .ToArray();

        return new ScanCatalogDeltaResult(
            generatedAt,
            SerializeWatermark(selectedBooks.RowVersion, generatedAt),
            books,
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

    private async Task<CatalogBookSelection> GetBooksToProjectAsync(
        CatalogWatermark? since,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var allBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.UpdatedAt <= generatedAt)
            .ToListAsync(cancellationToken);
        var changedBooks = since is null
            ? allBooks.ToArray()
            : allBooks.Where(book => HasChanged(book, since)).ToArray();
        var rowVersion = HighestRowVersion(allBooks, since?.RowVersion);

        if (since is null)
        {
            return new CatalogBookSelection(
                changedBooks.Where(book => !book.IsHiddenFromCatalog).ToArray(),
                rowVersion);
        }

        var recentWatchlistItems = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.AddedAt > since.AsOf && item.AddedAt <= generatedAt)
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

        var selected = changedBooks
            .Concat(watchlistStateChanged
                ? allBooks.Where(book => !book.IsHiddenFromCatalog)
                : allBooks.Where(book =>
                    !book.IsHiddenFromCatalog &&
                    recentWatchlistItems.Any(item => Matches(item, book))))
            .GroupBy(book => book.Id.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new CatalogBookSelection(selected, rowVersion);
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

    private static bool HasChanged(Book book, CatalogWatermark since)
    {
        if (book.RowVersion.Length > 0 && since.RowVersion.Length > 0)
        {
            return CompareRowVersions(book.RowVersion, since.RowVersion) > 0;
        }

        return book.UpdatedAt > since.AsOf;
    }

    private static byte[] HighestRowVersion(
        IEnumerable<Book> books,
        byte[]? previousRowVersion)
    {
        var highest = previousRowVersion ?? [];
        foreach (var book in books)
        {
            if (CompareRowVersions(book.RowVersion, highest) > 0)
            {
                highest = book.RowVersion;
            }
        }

        return highest.ToArray();
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

    private static bool Matches(WatchlistItem item, Book book)
    {
        return item.Scope switch
        {
            WatchlistItemScope.Edition => item.Isbn13 == book.Id,
            WatchlistItemScope.Work => item.WorkId is not null && item.WorkId == book.WorkId,
            _ => false,
        };
    }

    private sealed record CatalogWatermark(byte[] RowVersion, DateTime AsOf);

    private sealed record CatalogBookSelection(
        IReadOnlyList<Book> Books,
        byte[] RowVersion);
}
