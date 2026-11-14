using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SendDueFairDigests;

public sealed class SendDueFairDigestsCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<FairDigestOptions> options,
    IRecommendationSettings recommendationSettings,
    IFairDigestEmailSender emailSender,
    IFairDigestContentService contentService)
    : IRequestHandler<SendDueFairDigestsCommand, ErrorOr<FairDigestSendResult>>
{
    public async Task<ErrorOr<FairDigestSendResult>> Handle(
        SendDueFairDigestsCommand command,
        CancellationToken cancellationToken)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("FairDigest.InvalidClock", "The fair digest clock must be expressed in UTC.");
        }

        if (!options.Value.Enabled || !recommendationSettings.Enabled || !emailSender.IsEnabled)
        {
            return new FairDigestSendResult(true, 0, 0, 0, 0);
        }

        var dueCampaigns = await dbContext.FairDigestCampaigns
            .Where(campaign =>
                campaign.PlannedAtUtc <= nowUtc &&
                campaign.SuspendedAt == null &&
                campaign.StartedAt == null &&
                campaign.CompletedAt == null)
            .OrderBy(campaign => campaign.PlannedAtUtc)
            .ThenBy(campaign => campaign.Id)
            .ToListAsync(cancellationToken);

        var campaignCount = 0;
        var totalSent = 0;
        var totalSkipped = 0;
        var totalFailed = 0;
        foreach (var campaign in dueCampaigns)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reservations = await ReserveCampaignAsync(campaign, nowUtc, cancellationToken);
            if (reservations is null)
            {
                continue;
            }

            campaignCount++;
            var fairContext = await LoadFairContextAsync(campaign, cancellationToken);
            var sentCount = 0;
            var skippedCount = 0;
            var failedCount = 0;
            foreach (var (delivery, member) in reservations)
            {
                try
                {
                    var content = await contentService.BuildAsync(member, fairContext, cancellationToken);
                    if (content.IsError)
                    {
                        delivery.MarkFailed(content.Errors[0].Code);
                        failedCount++;
                        continue;
                    }

                    if (content.Value.SkippedNoContent)
                    {
                        delivery.MarkSkippedNoContent();
                        skippedCount++;
                        continue;
                    }

                    var email = member.Email;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        delivery.MarkFailed("MissingEmail");
                        failedCount++;
                        continue;
                    }

                    await emailSender.SendAsync(
                        new FairDigestEmailMessage(
                            campaign.CampaignId,
                            member.Id,
                            email,
                            member.Name?.FirstName,
                            fairContext.NextFair!,
                            content.Value),
                        cancellationToken);
                    var sentAt = dateTimeProvider.UtcNow;
                    if (sentAt.Kind != DateTimeKind.Utc)
                    {
                        throw new InvalidOperationException("The fair digest clock must be expressed in UTC.");
                    }

                    delivery.MarkSent(sentAt);
                    sentCount++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    delivery.MarkFailed(CreateFailureCode(exception));
                    failedCount++;
                }
            }

            campaign.Complete(nowUtc, sentCount, skippedCount, failedCount);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalSent += sentCount;
            totalSkipped += skippedCount;
            totalFailed += failedCount;
        }

        return new FairDigestSendResult(
            Disabled: false,
            campaignCount,
            totalSent,
            totalSkipped,
            totalFailed);
    }

    private async Task<IReadOnlyList<(FairDigestDelivery Delivery, User Member)>?> ReserveCampaignAsync(
        FairDigestCampaign campaign,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var currentCampaign = await dbContext.FairDigestCampaigns
            .SingleOrDefaultAsync(candidate => candidate.Id == campaign.Id, cancellationToken);
        if (currentCampaign is null ||
            currentCampaign.SuspendedAt is not null ||
            currentCampaign.StartedAt is not null ||
            currentCampaign.CompletedAt is not null ||
            currentCampaign.PlannedAtUtc > nowUtc)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        currentCampaign.Start(nowUtc);
        var recipients = await dbContext.Users
            .Where(user =>
                user.Email != null &&
                dbContext.MemberRecommendationPreferences.Any(preference =>
                    preference.UserId == user.Id && preference.FairDigestConsentedAt != null) &&
                dbContext.MemberFavoriteBooks.Any(favorite => favorite.UserId == user.Id) &&
                !dbContext.Watchlists.Any(watchlist =>
                    watchlist.Id == user.Id && watchlist.AlertStatus != WatchlistAlertStatus.Active))
            .ToListAsync(cancellationToken);
        var existingUserIds = await dbContext.FairDigestDeliveries
            .Where(delivery => delivery.CampaignId == campaign.CampaignId)
            .Select(delivery => delivery.UserId)
            .ToListAsync(cancellationToken);
        var existingIds = existingUserIds.Select(userId => userId.Value).ToHashSet();
        var reservations = new List<(FairDigestDelivery Delivery, User Member)>();
        foreach (var recipient in recipients.Where(user => !existingIds.Contains(user.Id.Value)))
        {
            var delivery = FairDigestDelivery.Create(campaign.CampaignId, recipient.Id);
            dbContext.FairDigestDeliveries.Add(delivery);
            reservations.Add((delivery, recipient));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return reservations;
    }

    private async Task<FairDigestScheduleResult> LoadFairContextAsync(
        FairDigestCampaign campaign,
        CancellationToken cancellationToken)
    {
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .OrderBy(assoEvent => assoEvent.DateStart)
            .ToListAsync(cancellationToken);
        var target = fairs.SingleOrDefault(assoEvent => assoEvent.Id.Value == campaign.AssoEventsId.Value);
        if (target is null)
        {
            return new FairDigestScheduleResult(null, null, null, null);
        }

        var previous = fairs
            .Where(assoEvent => assoEvent.DateStart < target.DateStart)
            .LastOrDefault();
        var previousFair = previous is null
            ? null
            : new FairDigestEvent(
                previous.Id.Value,
                previous.EventsType.Value,
                previous.DateStart,
                previous.DateEnd,
                previous.IsCancelled,
                previous.Name);
        DateTimeOffset? previousEnd = previousFair is null
            ? null
            : FairDigestSchedule.EndOfFairDay(previousFair.DateEnd ?? previousFair.DateStart);

        return new FairDigestScheduleResult(
            new FairDigestEvent(
                target.Id.Value,
                target.EventsType.Value,
                target.DateStart,
                target.DateEnd,
                target.IsCancelled,
                target.Name),
            previousFair,
            new DateTimeOffset(campaign.PlannedAtUtc, TimeSpan.Zero),
            previousEnd);
    }

    private static string CreateFailureCode(Exception exception)
    {
        var code = exception.GetType().Name;
        return code.Length <= 128 ? code : code[..128];
    }
}
