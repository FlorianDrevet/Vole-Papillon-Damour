using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.EraseExpired;

public sealed class EraseExpiredManualRequestersCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EraseExpiredManualRequestersCommand, int>
{
    public static readonly TimeSpan RetentionAfterLastClosure = TimeSpan.FromDays(90);

    public async Task<int> Handle(
        EraseExpiredManualRequestersCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The manual requester retention timestamp must be expressed in UTC.");
        }

        var cutoff = now - RetentionAfterLastClosure;
        var histories = await dbContext.ManualBookRequests
            .AsNoTracking()
            .GroupBy(request => request.RequesterId)
            .Select(group => new RequesterRequestHistory(
                group.Key,
                group.Any(request => request.Status == ManualBookRequestStatus.Searching ||
                                     request.Status == ManualBookRequestStatus.Found),
                group.Any(request => request.ClosedAt == null),
                group.Max(request => request.ClosedAt)))
            .ToDictionaryAsync(history => history.RequesterId, cancellationToken);

        var requesters = await dbContext.ManualRequesters
            .Where(requester => requester.ErasedAt == null)
            .ToListAsync(cancellationToken);

        var erasedCount = 0;
        foreach (var requester in requesters)
        {
            if (histories.TryGetValue(requester.Id, out var history))
            {
                if (history.HasOpenRequests ||
                    history.HasMissingClosureTimestamp ||
                    history.LastClosureAt is null ||
                    history.LastClosureAt > cutoff)
                {
                    continue;
                }
            }
            else if (requester.CreatedAt > cutoff)
            {
                continue;
            }

            requester.Erase(now);
            erasedCount++;
        }

        if (erasedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return erasedCount;
    }

    private sealed record RequesterRequestHistory(
        Guid RequesterId,
        bool HasOpenRequests,
        bool HasMissingClosureTimestamp,
        DateTime? LastClosureAt);
}
