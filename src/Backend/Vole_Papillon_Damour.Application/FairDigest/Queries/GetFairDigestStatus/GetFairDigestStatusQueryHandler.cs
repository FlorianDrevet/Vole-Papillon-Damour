using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.FairDigest.Commands.PlanFairDigest;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Queries.GetFairDigestStatus;

public sealed class GetFairDigestStatusQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<FairDigestOptions> options,
    IRecommendationSettings recommendationSettings,
    IFairDigestEmailSender emailSender)
    : IRequestHandler<GetFairDigestStatusQuery, ErrorOr<FairDigestStatusResult>>
{
    public async Task<ErrorOr<FairDigestStatusResult>> Handle(
        GetFairDigestStatusQuery query,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
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
                assoEvent.Name,
                FairDigestEvent.FormatAddress(assoEvent.Adresse))),
            new DateTimeOffset(nowUtc, TimeSpan.Zero),
            options.Value.DaysBefore,
            options.Value.SendHourParis);

        FairDigestCampaign? campaign = null;
        if (schedule.NextFair is not null)
        {
            campaign = await dbContext.FairDigestCampaigns
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.AssoEventsId == AssoEventsId.Create(schedule.NextFair.Id),
                    cancellationToken);
        }

        var estimatedRecipients = await dbContext.Users
            .AsNoTracking()
            .CountAsync(user =>
                user.Email != null &&
                dbContext.MemberRecommendationPreferences.Any(preference =>
                    preference.UserId == user.Id && preference.FairDigestConsentedAt != null) &&
                dbContext.MemberFavoriteBooks.Any(favorite => favorite.UserId == user.Id) &&
                !dbContext.Watchlists.Any(watchlist =>
                    watchlist.Id == user.Id && watchlist.AlertStatus != WatchlistAlertStatus.Active),
                cancellationToken);

        var lastReportCampaign = await dbContext.FairDigestCampaigns
            .AsNoTracking()
            .Where(candidate => candidate.CompletedAt != null)
            .OrderByDescending(candidate => candidate.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);
        string? lastReportFairName = null;
        if (lastReportCampaign is not null)
        {
            lastReportFairName = await dbContext.AssoEvents
                .AsNoTracking()
                .Where(assoEvent => assoEvent.Id == lastReportCampaign.AssoEventsId)
                .Select(assoEvent => assoEvent.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var lastReport = lastReportCampaign?.CompletedAt is { } completedAt
            ? new FairDigestCampaignReport(
                lastReportFairName ?? "Bourse aux livres",
                new DateTimeOffset(completedAt, TimeSpan.Zero),
                lastReportCampaign.SentCount,
                lastReportCampaign.SkippedCount,
                lastReportCampaign.FailedCount)
            : null;

        var enabled = options.Value.Enabled && recommendationSettings.Enabled && emailSender.IsEnabled;
        return new FairDigestStatusResult(
            enabled,
            schedule.NextFair?.Id,
            schedule.NextFair?.Name,
            schedule.NextFair?.DateStart,
            campaign?.CampaignId,
            campaign is null
                ? schedule.PlannedAtUtc
                : new DateTimeOffset(campaign.PlannedAtUtc, TimeSpan.Zero),
            campaign?.SuspendedAt is not null,
            estimatedRecipients,
            lastReport);
    }
}
