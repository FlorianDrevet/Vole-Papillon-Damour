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

        var requests = await dbContext.ManualBookRequests
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var requesters = await LoadRequestersAsync(requests, cancellationToken);
        var searching = requests.Where(request => request.Status == ManualBookRequestStatus.Searching).ToArray();
        var found = requests.Where(request => request.Status == ManualBookRequestStatus.Found).ToArray();
        var closed = requests.Where(request => request.Status is
            ManualBookRequestStatus.Given or ManualBookRequestStatus.Refused or ManualBookRequestStatus.Cancelled).ToArray();
        var overdueBefore = now.AddDays(-14);
        var overdueCount = found.Count(request => request.FoundAt < overdueBefore);

        IEnumerable<ManualBookRequest> selected = query.Tab switch
        {
            ManualRequestTab.Found => found
                .OrderBy(request => request.FoundAt ?? DateTime.MaxValue)
                .ThenBy(request => request.RequestedAt)
                .ThenBy(request => request.Id),
            ManualRequestTab.Searching => searching
                .OrderBy(request => request.RequestedAt)
                .ThenBy(request => request.Id),
            ManualRequestTab.Closed => closed
                .OrderByDescending(request => request.ClosedAt ?? DateTime.MinValue)
                .ThenBy(request => request.Id),
            _ => []
        };
        selected = ApplySearch(selected, requesters, query.Search);
        var matching = selected.ToArray();
        var totalCount = matching.Length;
        var pageRequests = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();
        var pageRequesters = pageRequests
            .Select(request => request.RequesterId)
            .Distinct()
            .Where(requesters.ContainsKey)
            .ToDictionary(requesterId => requesterId, requesterId => requesters[requesterId]);
        var items = await ManualRequestQueueProjection.BuildAsync(
            dbContext,
            pageRequests,
            pageRequesters,
            now,
            cancellationToken);

        return new ManualRequestQueueResult(
            items,
            totalCount,
            found.Length,
            searching.Length,
            closed.Length,
            overdueCount,
            query.Page,
            query.PageSize);
    }

    private async Task<Dictionary<Guid, ManualRequester>> LoadRequestersAsync(
        IReadOnlyCollection<ManualBookRequest> requests,
        CancellationToken cancellationToken)
    {
        var requesterIds = requests.Select(request => request.RequesterId).Distinct().ToArray();
        if (requesterIds.Length == 0)
        {
            return new Dictionary<Guid, ManualRequester>();
        }

        return await dbContext.ManualRequesters
            .AsNoTracking()
            .Where(requester => requesterIds.Contains(requester.Id))
            .ToDictionaryAsync(requester => requester.Id, cancellationToken);
    }

    private static IEnumerable<ManualBookRequest> ApplySearch(
        IEnumerable<ManualBookRequest> requests,
        IReadOnlyDictionary<Guid, ManualRequester> requesters,
        string? search)
    {
        var normalized = search?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return requests;
        }

        var digits = string.Concat(normalized.Where(char.IsAsciiDigit));
        return requests.Where(request =>
        {
            requesters.TryGetValue(request.RequesterId, out var requester);
            return Contains(request.Title, normalized) ||
                   Contains(requester?.FirstName, normalized) ||
                   Contains(requester?.LastName, normalized) ||
                   (digits.Length > 0 && requester?.Phone?.Contains(digits, StringComparison.Ordinal) == true);
        });
    }

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
}
