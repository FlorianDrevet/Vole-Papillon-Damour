using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;

internal static class ManualRequestQueueProjection
{
    public static async Task<IReadOnlyList<ManualRequestQueueItem>> BuildAsync(
        IProjectDbContext dbContext,
        IReadOnlyCollection<ManualBookRequest> requests,
        IReadOnlyDictionary<Guid, ManualRequester> requesters,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var actorIds = requests
            .SelectMany(request => new[] { request.FoundBy, request.ClosedBy })
            .Where(userId => userId is not null)
            .Select(userId => userId!)
            .Distinct()
            .ToArray();
        var actors = actorIds.Length == 0
            ? new Dictionary<UserId, string?>()
            : await dbContext.Users
                .AsNoTracking()
                .Where(user => actorIds.Contains(user.Id))
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.Name == null ? null : user.Name.FirstName,
                    cancellationToken);

        var foundIsbns = requests
            .Where(request => request.FoundIsbn13 is not null)
            .Select(request => request.FoundIsbn13!.Value)
            .Distinct()
            .ToArray();
        var foundBooks = foundIsbns.Length == 0
            ? new Dictionary<Isbn13, Book>()
            : await dbContext.Books
                .AsNoTracking()
                .Where(book => foundIsbns.Contains(book.Id))
                .ToDictionaryAsync(book => book.Id, cancellationToken);

        // One batch lookup per data kind for the whole page, instead of one round trip per item.
        var searchingRequests = requests.Where(request => request.Status == ManualBookRequestStatus.Searching).ToArray();
        var foundRequests = requests.Where(request => request.Status == ManualBookRequestStatus.Found).ToArray();
        var stocks = await ManualRequestStock.GetManyAsync(dbContext, searchingRequests, cancellationToken);
        var watcherKeys = foundRequests
            .Where(request => request.FoundIsbn13 is not null)
            .Select(request => (request.Id, request.FoundIsbn13!.Value, request.WorkId))
            .ToArray();
        var watcherCounts = await ManualRequestWatchers.CountManyAsync(dbContext, watcherKeys, cancellationToken);
        var releasePlans = await ManualRequestRelease.PlanManyAsync(dbContext, foundRequests, now, cancellationToken);

        var items = new List<ManualRequestQueueItem>(requests.Count);
        foreach (var request in requests)
        {
            requesters.TryGetValue(request.RequesterId, out var requester);
            var erased = requester?.IsErased ?? true;
            var quantityAvailable = 0;
            var quantityAnnounced = 0;
            var watcherCount = 0;
            ManualRequestReleaseMode? releaseMode = null;
            DateTimeOffset? releaseFairStart = null;

            if (request.Status == ManualBookRequestStatus.Searching)
            {
                var stock = stocks[request.Id];
                quantityAvailable = stock.QuantityAvailable;
                quantityAnnounced = stock.QuantityAnnounced;
            }
            else if (request.Status == ManualBookRequestStatus.Found)
            {
                watcherCount = watcherCounts.GetValueOrDefault(request.Id);
                var release = releasePlans[request.Id];
                releaseMode = release.Mode;
                releaseFairStart = release.FairStart;
            }

            foundBooks.TryGetValue(request.FoundIsbn13 ?? default, out var foundBook);
            items.Add(new ManualRequestQueueItem(
                request.Id,
                request.RequesterId,
                erased ? null : requester?.LastName,
                erased ? null : requester?.FirstName,
                erased ? null : requester?.Phone,
                erased,
                request.Scope,
                request.WorkId,
                request.Isbn13?.Value,
                request.Title,
                request.Authors,
                request.Publisher,
                request.PublicationYear,
                request.RequestedAt,
                request.Status,
                request.FoundOrigin,
                request.FoundIsbn13?.Value,
                foundBook?.Publisher,
                foundBook?.PublicationYear,
                request.FoundAt,
                request.FoundBy is { } foundBy ? actors.GetValueOrDefault(foundBy) : null,
                quantityAvailable,
                quantityAnnounced,
                watcherCount,
                releaseMode,
                releaseFairStart,
                request.ClosedAt,
                request.ClosedBy is { } closedBy ? actors.GetValueOrDefault(closedBy) : null));
        }

        return items;
    }
}
