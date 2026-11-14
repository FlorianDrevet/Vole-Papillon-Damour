using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public static class FairDigestArrivalSelector
{
    public static async Task<IReadOnlySet<string>> ReadArrivedIsbnsAsync(
        IProjectDbContext dbContext,
        DateTime arrivedAfterUtc,
        CancellationToken cancellationToken)
    {
        var entries = await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                (movement.Type == BookMovementType.DirectEntry ||
                 movement.Type == BookMovementType.AnnouncementEntry) &&
                movement.Quantity > 0)
            .GroupBy(movement => movement.Isbn13)
            .Select(group => new
            {
                Isbn13 = group.Key,
                FirstEntryAt = group.Min(movement => movement.OccurredAt),
            })
            .Where(entry => entry.FirstEntryAt > arrivedAfterUtc)
            .ToListAsync(cancellationToken);
        return entries.Select(entry => entry.Isbn13.Value).ToHashSet(StringComparer.Ordinal);
    }
}
