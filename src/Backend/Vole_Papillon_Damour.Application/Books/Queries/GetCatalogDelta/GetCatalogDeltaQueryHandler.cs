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
        if (query.Since is {Kind: not DateTimeKind.Utc})
        {
            return Error.Validation(
                "Books.InvalidCatalogWatermark",
                "The catalog watermark must be expressed in UTC.");
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var selectedBooks = await GetBooksToProjectAsync(
            query.Since,
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

        var books = selectedBooks
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
            generatedAt,
            books,
            AssociationSettingsResult.From(settings));
    }

    private async Task<IReadOnlyList<Book>> GetBooksToProjectAsync(
        DateTime? since,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var changedBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                book.UpdatedAt <= generatedAt &&
                (since == null || book.UpdatedAt > since.Value))
            .ToListAsync(cancellationToken);

        if (since is null)
        {
            return changedBooks.Where(book => !book.IsHiddenFromCatalog).ToArray();
        }

        var recentWatchlistItems = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.AddedAt > since.Value && item.AddedAt <= generatedAt)
            .ToListAsync(cancellationToken);
        var watchlistStateChanged = await dbContext.Watchlists
            .AsNoTracking()
            .AnyAsync(
                watchlist => watchlist.UpdatedAt > since.Value && watchlist.UpdatedAt <= generatedAt,
                cancellationToken);
        if (recentWatchlistItems.Count == 0 && !watchlistStateChanged)
        {
            return changedBooks;
        }

        var allBooks = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.UpdatedAt <= generatedAt && !book.IsHiddenFromCatalog)
            .ToListAsync(cancellationToken);
        var selected = changedBooks
            .Concat(watchlistStateChanged
                ? allBooks
                : allBooks.Where(book => recentWatchlistItems.Any(item => Matches(item, book))))
            .GroupBy(book => book.Id.Value, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return selected;
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
}
