using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;

public sealed class GetManualRequestQueueQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetManualRequestQueueQuery, ErrorOr<ManualRequestQueueResult>>
{
    private const int MaxPageSize = 100;
    private const string LikeEscapeCharacter = "\\";

    public async Task<ErrorOr<ManualRequestQueueResult>> Handle(
        GetManualRequestQueueQuery query,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(query.Tab))
        {
            return Error.Validation("ManualRequest.InvalidTab", "The manual request tab is not supported.");
        }

        if (query.Page < 1 || query.PageSize is < 1 or > MaxPageSize ||
            query.Page > int.MaxValue / query.PageSize)
        {
            return Error.Validation("ManualRequest.InvalidPage", "The manual request queue page is invalid.");
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var requests = dbContext.ManualBookRequests.AsNoTracking();
        var statusCounts = await requests
            .GroupBy(request => request.Status)
            .Select(group => new StatusCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var foundCount = CountStatuses(statusCounts, ManualBookRequestStatus.Found);
        var searchingCount = CountStatuses(statusCounts, ManualBookRequestStatus.Searching);
        var closedCount = CountStatuses(
            statusCounts,
            ManualBookRequestStatus.Given,
            ManualBookRequestStatus.Refused,
            ManualBookRequestStatus.Cancelled);
        var overdueBefore = now.AddDays(-14);
        var overdueCount = await requests.CountAsync(
            request => request.Status == ManualBookRequestStatus.Found && request.FoundAt < overdueBefore,
            cancellationToken);

        var tabRequests = query.Tab switch
        {
            ManualRequestTab.Found => requests.Where(request => request.Status == ManualBookRequestStatus.Found),
            ManualRequestTab.Searching => requests.Where(request => request.Status == ManualBookRequestStatus.Searching),
            ManualRequestTab.Closed => requests.Where(request =>
                request.Status == ManualBookRequestStatus.Given ||
                request.Status == ManualBookRequestStatus.Refused ||
                request.Status == ManualBookRequestStatus.Cancelled),
            _ => requests.Where(_ => false)
        };
        tabRequests = ApplySearch(tabRequests, query.Search);

        var totalCount = await tabRequests.CountAsync(cancellationToken);
        // Nulls sort last for Found (FoundAt ?? MaxValue) and for Closed (ClosedAt ?? MinValue, descending).
        IOrderedQueryable<ManualBookRequest> ordered = query.Tab switch
        {
            ManualRequestTab.Found => tabRequests
                .OrderBy(request => request.FoundAt == null ? 1 : 0)
                .ThenBy(request => request.FoundAt)
                .ThenBy(request => request.RequestedAt)
                .ThenBy(request => request.Id),
            ManualRequestTab.Searching => tabRequests
                .OrderBy(request => request.RequestedAt)
                .ThenBy(request => request.Id),
            _ => tabRequests
                .OrderBy(request => request.ClosedAt == null ? 1 : 0)
                .ThenByDescending(request => request.ClosedAt)
                .ThenBy(request => request.Id)
        };
        var pageRequests = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var pageRequesterIds = pageRequests.Select(request => request.RequesterId).Distinct().ToArray();
        var pageRequesters = pageRequesterIds.Length == 0
            ? new Dictionary<Guid, ManualRequester>()
            : await dbContext.ManualRequesters
                .AsNoTracking()
                .Where(requester => pageRequesterIds.Contains(requester.Id))
                .ToDictionaryAsync(requester => requester.Id, cancellationToken);
        var items = await ManualRequestQueueProjection.BuildAsync(
            dbContext,
            pageRequests,
            pageRequesters,
            now,
            cancellationToken);

        return new ManualRequestQueueResult(
            items,
            totalCount,
            foundCount,
            searchingCount,
            closedCount,
            overdueCount,
            query.Page,
            query.PageSize);
    }

    private IQueryable<ManualBookRequest> ApplySearch(IQueryable<ManualBookRequest> requests, string? search)
    {
        var normalized = search?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return requests;
        }

        // LIKE follows the column collation (case-insensitive); wildcards in the term are literals.
        var pattern = $"%{EscapeLikePattern(normalized)}%";
        var digits = string.Concat(normalized.Where(char.IsAsciiDigit));
        var includePhone = digits.Length > 0;
        var phonePattern = $"%{digits}%";

        return requests.Where(request =>
            EF.Functions.Like(request.Title ?? string.Empty, pattern, LikeEscapeCharacter) ||
            dbContext.ManualRequesters.Any(requester =>
                requester.Id == request.RequesterId &&
                (EF.Functions.Like(requester.FirstName ?? string.Empty, pattern, LikeEscapeCharacter) ||
                 EF.Functions.Like(requester.LastName ?? string.Empty, pattern, LikeEscapeCharacter) ||
                 (includePhone && EF.Functions.Like(requester.Phone ?? string.Empty, phonePattern, LikeEscapeCharacter)))));
    }

    private static int CountStatuses(
        IReadOnlyCollection<StatusCount> statusCounts,
        params ManualBookRequestStatus[] statuses)
    {
        return statusCounts
            .Where(count => statuses.Contains(count.Status))
            .Sum(count => count.Count);
    }

    private sealed record StatusCount(ManualBookRequestStatus Status, int Count);

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter, StringComparison.Ordinal)
            .Replace("%", LikeEscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", LikeEscapeCharacter + "_", StringComparison.Ordinal)
            .Replace("[", LikeEscapeCharacter + "[", StringComparison.Ordinal);
    }
}
