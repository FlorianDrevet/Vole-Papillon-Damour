using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
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
    private const string LikeEscapeCharacter = "\\";

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

        var members = dbContext.Users.AsNoTracking();
        if (status is { } requestedStatus)
        {
            members = members.Where(user => dbContext.Watchlists.Any(watchlist =>
                watchlist.Id == user.Id && watchlist.AlertStatus == requestedStatus));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Same fields as before: email, external id and "first last" display name.
            // LIKE follows the column collation; wildcards in the term are literals.
            var pattern = $"%{EscapeLikePattern(search)}%";
            members = members.Where(user =>
                EF.Functions.Like(user.Email ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(user.ExternalId ?? string.Empty, pattern, LikeEscapeCharacter) ||
                EF.Functions.Like(
                    (user.Name.FirstName ?? string.Empty) + " " + (user.Name.LastName ?? string.Empty),
                    pattern,
                    LikeEscapeCharacter));
        }

        var totalCount = await members.CountAsync(cancellationToken);
        // Computed in 64 bits: an overflowing page number has no rows instead of wrapping around.
        var offset = (query.Page - 1) * (long)query.PageSize;
        var pageUsers = offset >= totalCount
            ? new List<UserEntity>()
            : await members
                .OrderByDescending(user => user.LastSeenAt)
                .ThenBy(user => user.Email)
                .ThenBy(user => user.Id)
                .Skip((int)offset)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

        var pageIds = pageUsers.Select(user => user.Id).ToArray();
        var watchlists = pageIds.Length == 0
            ? new Dictionary<UserId, WatchlistEntity>()
            : await dbContext.Watchlists
                .AsNoTracking()
                .Where(watchlist => pageIds.Contains(watchlist.Id))
                .ToDictionaryAsync(watchlist => watchlist.Id, cancellationToken);
        var itemCounts = pageIds.Length == 0
            ? new Dictionary<UserId, int>()
            : (await dbContext.WatchlistItems
                .AsNoTracking()
                .Where(item => pageIds.Contains(item.UserId))
                .GroupBy(item => item.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
                .ToDictionary(group => group.UserId, group => group.Count);
        var historyCounts = pageIds.Length == 0
            ? new Dictionary<UserId, int>()
            : (await dbContext.UserAlertHistories
                .AsNoTracking()
                .Where(history => pageIds.Contains(history.UserId))
                .GroupBy(history => history.UserId)
                .Select(group => new { UserId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
                .ToDictionary(group => group.UserId, group => group.Count);

        var rows = pageUsers
            .Select(user => BuildSummary(
                user,
                watchlists.GetValueOrDefault(user.Id),
                itemCounts.GetValueOrDefault(user.Id),
                historyCounts.GetValueOrDefault(user.Id)))
            .ToArray();
        return new AdminMemberPageResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            rows,
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
        return BuildSummary(
            user,
            watchlist,
            items.Count(item => item.UserId == user.Id),
            histories.Count(history => history.UserId == user.Id));
    }

    internal static AdminMemberSummaryResult BuildSummary(
        UserEntity user,
        WatchlistEntity? watchlist,
        int itemCount,
        int historyCount)
    {
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
            itemCount,
            historyCount);
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

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)
            .Replace("[", LikeEscapeCharacter + "[", StringComparison.Ordinal);
    }
}
