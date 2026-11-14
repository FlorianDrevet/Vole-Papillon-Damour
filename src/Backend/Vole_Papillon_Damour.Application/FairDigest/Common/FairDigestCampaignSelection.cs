using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public static class FairDigestCampaignSelection
{
    public static async Task<FairDigestCampaign?> FindNextAsync(
        IProjectDbContext dbContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var nextFairId = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .Where(fair => fair.DateStart > new DateTimeOffset(nowUtc, TimeSpan.Zero))
            .OrderBy(fair => fair.DateStart)
            .Select(fair => fair.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (nextFairId is null)
        {
            return null;
        }

        return await dbContext.FairDigestCampaigns
            .SingleOrDefaultAsync(campaign => campaign.AssoEventsId == nextFairId, cancellationToken);
    }
}
