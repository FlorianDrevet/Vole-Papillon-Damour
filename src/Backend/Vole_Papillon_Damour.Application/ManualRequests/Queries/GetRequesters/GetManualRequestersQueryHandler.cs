using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequesters;

public sealed class GetManualRequestersQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<GetManualRequestersQuery, ErrorOr<ManualRequesterPageResult>>
{
    private const int MaxPageSize = 100;

    public async Task<ErrorOr<ManualRequesterPageResult>> Handle(
        GetManualRequestersQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > MaxPageSize ||
            query.Page > int.MaxValue / query.PageSize)
        {
            return Error.Validation("ManualRequest.InvalidPage", "The manual requester page is invalid.");
        }

        var requesters = dbContext.ManualRequesters
            .AsNoTracking()
            .Where(requester => requester.ErasedAt == null);

        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var normalizedSearch = search.ToLowerInvariant();
            var phoneDigits = string.Concat(search.Where(char.IsAsciiDigit));
            requesters = requesters.Where(requester =>
                (requester.FirstName != null && requester.FirstName.ToLower().Contains(normalizedSearch)) ||
                (requester.LastName != null && requester.LastName.ToLower().Contains(normalizedSearch)) ||
                (requester.FirstName != null && requester.LastName != null &&
                 (requester.FirstName + " " + requester.LastName).ToLower().Contains(normalizedSearch)) ||
                (requester.FirstName != null && requester.LastName != null &&
                 (requester.LastName + " " + requester.FirstName).ToLower().Contains(normalizedSearch)) ||
                (phoneDigits.Length > 0 && requester.Phone != null && requester.Phone.Contains(phoneDigits)));
        }

        var totalCount = await requesters.CountAsync(cancellationToken);
        var pageRequesters = await requesters
            .OrderBy(requester => requester.LastName)
            .ThenBy(requester => requester.FirstName)
            .ThenBy(requester => requester.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(requester => new
            {
                requester.Id,
                requester.LastName,
                requester.FirstName,
                requester.Phone,
            })
            .ToListAsync(cancellationToken);

        var requesterIds = pageRequesters.Select(requester => requester.Id).ToArray();
        var counts = requesterIds.Length == 0
            ? new Dictionary<Guid, RequestCounts>()
            : await dbContext.ManualBookRequests
                .AsNoTracking()
                .Where(request => requesterIds.Contains(request.RequesterId))
                .GroupBy(request => request.RequesterId)
                .Select(group => new RequestCounts(
                    group.Key,
                    group.Count(),
                    group.Count(request => request.Status == ManualBookRequestStatus.Searching),
                    group.Count(request => request.Status == ManualBookRequestStatus.Found)))
                .ToDictionaryAsync(count => count.RequesterId, cancellationToken);

        var items = pageRequesters.Select(requester =>
        {
            counts.TryGetValue(requester.Id, out var requestCounts);
            return new ManualRequesterListItem(
                requester.Id,
                requester.LastName ?? string.Empty,
                requester.FirstName ?? string.Empty,
                requester.Phone ?? string.Empty,
                requestCounts?.RequestCount ?? 0,
                requestCounts?.SearchingCount ?? 0,
                requestCounts?.FoundCount ?? 0);
        }).ToArray();

        return new ManualRequesterPageResult(items, totalCount, query.Page, query.PageSize);
    }

    private sealed record RequestCounts(Guid RequesterId, int RequestCount, int SearchingCount, int FoundCount);
}
