using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.PlanFairDigest;

public sealed class PlanFairDigestCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<FairDigestOptions> options,
    IRecommendationSettings recommendationSettings)
    : IRequestHandler<PlanFairDigestCommand, ErrorOr<FairDigestPlanResult>>
{
    public async Task<ErrorOr<FairDigestPlanResult>> Handle(
        PlanFairDigestCommand command,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
        }

        if (!options.Value.Enabled || !recommendationSettings.Enabled)
        {
            return new FairDigestPlanResult(FairDigestPlanStatus.Disabled, null, null, null);
        }

        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .OrderBy(assoEvent => assoEvent.DateStart)
            .ToListAsync(cancellationToken);
        var schedule = FairDigestSchedule.Calculate(
            fairs.Select(assoEvent => new FairDigestEvent(
                assoEvent.Id.Value,
                assoEvent.EventsType.Value,
                assoEvent.DateStart,
                assoEvent.DateEnd,
                assoEvent.IsCancelled,
                assoEvent.Name)),
            new DateTimeOffset(nowUtc, TimeSpan.Zero),
            options.Value.DaysBefore,
            options.Value.SendHourParis);

        if (schedule.NextFair is null)
        {
            return new FairDigestPlanResult(FairDigestPlanStatus.NoUpcomingFair, null, null, null);
        }

        if (schedule.PlannedAtUtc is null)
        {
            return new FairDigestPlanResult(
                FairDigestPlanStatus.TooLate,
                null,
                schedule.NextFair.Id,
                null);
        }

        var assoEventsId = AssoEventsId.Create(schedule.NextFair.Id);
        var existing = await dbContext.FairDigestCampaigns
            .SingleOrDefaultAsync(campaign => campaign.AssoEventsId == assoEventsId, cancellationToken);
        if (existing is not null)
        {
            return Existing(existing);
        }

        var campaign = FairDigestCampaign.Create(assoEventsId, schedule.PlannedAtUtc.Value.UtcDateTime);
        dbContext.FairDigestCampaigns.Add(campaign);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrentlyCreated = await dbContext.FairDigestCampaigns
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.AssoEventsId == assoEventsId, cancellationToken);
            if (concurrentlyCreated is null)
            {
                throw;
            }

            return Existing(concurrentlyCreated);
        }

        return new FairDigestPlanResult(
            FairDigestPlanStatus.Planned,
            campaign.CampaignId,
            campaign.AssoEventsId.Value,
            new DateTimeOffset(campaign.PlannedAtUtc, TimeSpan.Zero));
    }

    private static FairDigestPlanResult Existing(FairDigestCampaign campaign) =>
        new(
            FairDigestPlanStatus.AlreadyPlanned,
            campaign.CampaignId,
            campaign.AssoEventsId.Value,
            new DateTimeOffset(campaign.PlannedAtUtc, TimeSpan.Zero));
}
