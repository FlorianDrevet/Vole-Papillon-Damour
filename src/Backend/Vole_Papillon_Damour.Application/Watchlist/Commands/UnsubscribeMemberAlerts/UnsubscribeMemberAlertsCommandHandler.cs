using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.UnsubscribeMemberAlerts;

public sealed class UnsubscribeMemberAlertsCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UnsubscribeMemberAlertsCommand, ErrorOr<UnsubscribeMemberAlertsResult>>
{
    public async Task<ErrorOr<UnsubscribeMemberAlertsResult>> Handle(
        UnsubscribeMemberAlertsCommand command,
        CancellationToken cancellationToken)
    {
        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Watchlist.InvalidClock",
                "The watchlist clock must be expressed in UTC.");
        }

        var memberId = UserId.Create(command.MemberId);
        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(
            candidate => candidate.Id == memberId,
            cancellationToken);
        if (watchlist is null)
        {
            return new UnsubscribeMemberAlertsResult(
                UnsubscribeMemberAlertsOutcome.IgnoredUnknownMember);
        }

        // Blocked is an administrative decision that outranks a self-service
        // opt-out, so it is left alone rather than downgraded to Suspended.
        if (watchlist.AlertStatus != WatchlistAlertStatus.Active)
        {
            return new UnsubscribeMemberAlertsResult(
                UnsubscribeMemberAlertsOutcome.AlreadyUnsubscribed);
        }

        watchlist.SuspendAlerts(updatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new UnsubscribeMemberAlertsResult(UnsubscribeMemberAlertsOutcome.Unsubscribed);
    }
}
