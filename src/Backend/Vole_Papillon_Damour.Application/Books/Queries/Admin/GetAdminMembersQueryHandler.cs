using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;
using UserEntity = Vole_Papillon_Damour.Domain.UserAggregate.User;
using WatchlistEntity = Vole_Papillon_Damour.Domain.WatchlistAggregate.Watchlist;
using WatchlistItemEntity = Vole_Papillon_Damour.Domain.WatchlistAggregate.WatchlistItem;
using UserAlertHistoryEntity = Vole_Papillon_Damour.Domain.WatchlistAggregate.UserAlertHistory;
using UserName = Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.Name;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetAdminMembersQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetAdminMembersQuery, ErrorOr<AdminMemberPageResult>>
{
    public async Task<ErrorOr<AdminMemberPageResult>> Handle(
        GetAdminMembersQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page <= 0 || query.PageSize is <= 0 or > 200)
        {
            return Errors.Book.InvalidAdminPage();
        }

        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        WatchlistAlertStatus? status = null;
        if (!string.IsNullOrWhiteSpace(query.AlertStatus))
        {
            if (!Enum.TryParse<WatchlistAlertStatus>(query.AlertStatus, true, out var parsed) ||
                !Enum.IsDefined(parsed))
            {
                return Error.Validation("Book.InvalidAlertStatus", "The member alert status is not supported.");
            }

            status = parsed;
        }

        var users = await dbContext.Users.AsNoTracking().ToListAsync(cancellationToken);
        var watchlists = await dbContext.Watchlists.AsNoTracking().ToListAsync(cancellationToken);
        var items = await dbContext.WatchlistItems.AsNoTracking().ToListAsync(cancellationToken);
        var histories = await dbContext.UserAlertHistories.AsNoTracking().ToListAsync(cancellationToken);
        var search = query.Search?.Trim();
        var rows = users
            .Where(user => !status.HasValue ||
                           watchlists.Any(watchlist =>
                               watchlist.Id == user.Id && watchlist.AlertStatus == status.Value))
            .Where(user => string.IsNullOrWhiteSpace(search) || Matches(user, search!))
            .OrderByDescending(user => user.LastSeenAt)
            .ThenBy(user => user.Email)
            .Select(user => BuildSummary(user, watchlists, items, histories))
            .ToList();
        var totalCount = rows.Count;
        return new AdminMemberPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            rows.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray(),
            totalCount,
            query.Page,
            query.PageSize);
    }

    internal static AdminMemberSummaryResult BuildSummary(
        UserEntity user,
        IReadOnlyCollection<WatchlistEntity> watchlists,
        IReadOnlyCollection<WatchlistItemEntity> items,
        IReadOnlyCollection<UserAlertHistoryEntity> histories)
    {
        var watchlist = watchlists.SingleOrDefault(candidate => candidate.Id == user.Id);
        return new AdminMemberSummaryResult(
            user.Id.Value,
            user.ExternalId,
            user.Email,
            GetDisplayName(user.Name),
            new DateTimeOffset(user.CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(user.LastSeenAt, TimeSpan.Zero),
            user.AnonymizedAt is { } anonymizedAt
                ? new DateTimeOffset(anonymizedAt, TimeSpan.Zero)
                : null,
            watchlist?.AlertStatus.ToString() ?? "None",
            watchlist?.BounceCount ?? 0,
            items.Count(item => item.UserId == user.Id),
            histories.Count(history => history.UserId == user.Id));
    }

    internal static string? GetDisplayName(UserName? name)
    {
        if (name is null)
        {
            return null;
        }

        var displayName = $"{name.FirstName} {name.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }

    private static bool Matches(UserEntity user, string search)
    {
        return user.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
               user.ExternalId?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
               GetDisplayName(user.Name)?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
    }
}
