using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Queries.GetMyWatchlist;

public sealed class GetMyWatchlistQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetMyWatchlistQuery, ErrorOr<MyWatchlistResult>>
{
    public async Task<ErrorOr<MyWatchlistResult>> Handle(
        GetMyWatchlistQuery query,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            query.ExternalId,
            query.Email,
            cancellationToken);
        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Watchlist.InvalidClock",
                "The watchlist clock must be expressed in UTC.");
        }

        var watchlist = await dbContext.Watchlists
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == user.Id,
                cancellationToken);
        if (watchlist is null)
        {
            return new MyWatchlistResult(
                new DateTimeOffset(generatedAt, TimeSpan.Zero),
                WatchlistAlertStatus.Active,
                0,
                []);
        }

        var items = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .OrderBy(item => item.AddedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var isbnValues = items
            .Where(item => item.Isbn13 is not null)
            .Select(item => item.Isbn13!.Value)
            .ToHashSet();
        var workIds = items
            .Where(item => item.WorkId is not null)
            .Select(item => item.WorkId!)
            .ToHashSet(StringComparer.Ordinal);

        var books = await dbContext.Books
            .AsNoTracking()
            .Where(book =>
                (!book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null) &&
                (isbnValues.Contains(book.Id) ||
                 (book.WorkId != null && workIds.Contains(book.WorkId))))
            .ToListAsync(cancellationToken);
        var bookIsbnValues = books.Select(book => book.Id).ToHashSet();
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement => bookIsbnValues.Contains(announcement.Isbn13))
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var publicBooks = PublicCatalogProjector.Project(
            books,
            announcements,
            fairs,
            generatedAt);

        var histories = await dbContext.UserAlertHistories
            .AsNoTracking()
            .Where(history => history.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var resultItems = items
            .Select(item =>
            {
                var matchingBooks = publicBooks
                    .Where(book => item.Scope == WatchlistItemScope.Edition
                        ? book.Isbn13 == item.Isbn13!.Value.Value
                        : book.WorkId == item.WorkId)
                    .OrderByDescending(book => book.QuantityAvailable > 0)
                    .ThenBy(book => book.PublicationYear)
                    .ThenBy(book => book.Isbn13, StringComparer.Ordinal)
                    .ToArray();
                var selectedBook = matchingBooks.FirstOrDefault();
                var matchingIsbns = matchingBooks
                    .Select(book => book.Isbn13)
                    .ToHashSet(StringComparer.Ordinal);
                var lastAlertAt = histories
                    .Where(history =>
                        item.Scope == WatchlistItemScope.Edition
                            ? history.Isbn13.Value == item.Isbn13!.Value.Value
                            : matchingIsbns.Contains(history.Isbn13.Value))
                    .Select(history => (DateTimeOffset?)new DateTimeOffset(history.SentAt, TimeSpan.Zero))
                    .OrderByDescending(value => value)
                    .FirstOrDefault();

                return new MyWatchlistItemResult(
                    item.Id,
                    item.Scope,
                    item.WorkId,
                    item.Isbn13?.Value,
                    selectedBook,
                    new DateTimeOffset(item.AddedAt, TimeSpan.Zero),
                    lastAlertAt);
            })
            .ToArray();

        return new MyWatchlistResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            watchlist.AlertStatus,
            watchlist.BounceCount,
            resultItems);
    }
}
