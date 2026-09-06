using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using UserIdValue = Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminMemberQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminMemberQuery, ErrorOr<AdminMemberDetailResult>>
{
    public async Task<ErrorOr<AdminMemberDetailResult>> Handle(
        GetAdminMemberQuery query,
        CancellationToken cancellationToken)
    {
        if (query.MemberId == Guid.Empty)
        {
            return Errors.Book.MemberNotFound(query.MemberId);
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var userId = UserIdValue.Create(query.MemberId);
        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        if (user is null)
        {
            return Errors.Book.MemberNotFound(query.MemberId);
        }

        var watchlist = await dbContext.Watchlists.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == userId,
            cancellationToken);
        var items = await dbContext.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.AddedAt)
            .ToListAsync(cancellationToken);
        var histories = await dbContext.UserAlertHistories
            .AsNoTracking()
            .Where(history => history.UserId == userId)
            .OrderByDescending(history => history.SentAt)
            .ToListAsync(cancellationToken);
        var books = await dbContext.Books.AsNoTracking().ToListAsync(cancellationToken);
        var announcements = await dbContext.BookAnnouncements.AsNoTracking().ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents.AsNoTracking().ToListAsync(cancellationToken);
        var publicBooks = PublicCatalogProjector.Project(books, announcements, fairs, generatedAt);

        var summary = GetAdminMembersQueryHandler.BuildSummary(
            user,
            watchlist is null ? [] : [watchlist],
            items,
            histories);
        var watchlistRows = items.Select(item =>
        {
            var matchingBooks = publicBooks
                .Where(book => item.Scope == WatchlistItemScope.Edition
                    ? book.Isbn13 == item.Isbn13!.Value.Value
                    : book.WorkId == item.WorkId)
                .OrderByDescending(book => book.QuantityAvailable > 0)
                .ThenBy(book => book.Isbn13, StringComparer.Ordinal)
                .ToArray();
            var selected = matchingBooks.FirstOrDefault();
            var matchingIsbns = matchingBooks.Select(book => book.Isbn13).ToHashSet(StringComparer.Ordinal);
            var lastAlertAt = histories
                .Where(history => item.Scope == WatchlistItemScope.Edition
                    ? history.Isbn13.Value == item.Isbn13!.Value.Value
                    : matchingIsbns.Contains(history.Isbn13.Value))
                .Select(history => (DateTimeOffset?)new DateTimeOffset(history.SentAt, TimeSpan.Zero))
                .FirstOrDefault();
            return new AdminMemberWatchlistItemResult(
                item.Id,
                item.Scope.ToString(),
                item.WorkId,
                item.Isbn13?.Value,
                selected?.Title,
                selected?.Authors,
                selected?.QuantityAvailable ?? 0,
                selected?.QuantityAnnounced ?? 0,
                new DateTimeOffset(item.AddedAt, TimeSpan.Zero),
                lastAlertAt);
        }).ToArray();

        var historyIsbns = histories.Select(history => history.Isbn13).ToArray();
        var historyBooks = books
            .Where(book => historyIsbns.Contains(book.Id))
            .ToDictionary(book => book.Id, book => book.Title);
        var alertRows = histories.Select(history => new AdminMemberAlertHistoryResult(
            history.Id,
            history.Isbn13.Value,
            historyBooks.GetValueOrDefault(history.Isbn13),
            new DateTimeOffset(history.SentAt, TimeSpan.Zero),
            history.OutboxMessageId)).ToArray();

        return new AdminMemberDetailResult(summary, watchlistRows, alertRows);
    }
}
