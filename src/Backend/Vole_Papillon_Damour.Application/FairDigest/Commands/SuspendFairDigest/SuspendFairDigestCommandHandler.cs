using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SuspendFairDigest;

public sealed class SuspendFairDigestCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SuspendFairDigestCommand, ErrorOr<FairDigestCampaignOperationResult>>
{
    public async Task<ErrorOr<FairDigestCampaignOperationResult>> Handle(
        SuspendFairDigestCommand command,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
        }

        var campaign = await FairDigestCampaignSelection.FindNextAsync(dbContext, nowUtc, cancellationToken);
        if (campaign is null)
        {
            return Error.NotFound("FairDigest.CampaignNotFound", "No campaign exists for the next book fair.");
        }

        if (campaign.StartedAt is not null || campaign.CompletedAt is not null)
        {
            return Error.Conflict("FairDigest.CampaignAlreadyStarted", "A started campaign cannot be suspended.");
        }

        if (campaign.SuspendedAt is not null)
        {
            return new FairDigestCampaignOperationResult(
                campaign.CampaignId,
                Changed: false,
                new DateTimeOffset(campaign.SuspendedAt.Value, TimeSpan.Zero));
        }

        try
        {
            campaign.Suspend(command.AdministratorId, nowUtc);
        }
        catch (InvalidOperationException)
        {
            return Error.Conflict("FairDigest.CampaignAlreadyStarted", "A started campaign cannot be suspended.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new FairDigestCampaignOperationResult(
            campaign.CampaignId,
            Changed: true,
            new DateTimeOffset(nowUtc, TimeSpan.Zero));
    }
}
