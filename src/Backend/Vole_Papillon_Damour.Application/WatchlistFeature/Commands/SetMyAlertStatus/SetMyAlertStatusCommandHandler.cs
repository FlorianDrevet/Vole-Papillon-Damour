using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.SetMyAlertStatus;

public sealed class SetMyAlertStatusCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetMyAlertStatusCommand, ErrorOr<MyAlertPreferencesResult>>
{
    public async Task<ErrorOr<MyAlertPreferencesResult>> Handle(
        SetMyAlertStatusCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            cancellationToken);
        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Watchlist.InvalidClock",
                "The watchlist clock must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(
            candidate => candidate.Id == user.Id,
            cancellationToken);
        if (watchlist is null)
        {
            watchlist = Watchlist.Create(user.Id, updatedAt);
            dbContext.Watchlists.Add(watchlist);
        }

        if (command.Enabled && watchlist.AlertStatus == WatchlistAlertStatus.Blocked)
        {
            return Errors.Watchlist.AlertsBlocked();
        }

        var changed = command.Enabled
            ? watchlist.AlertStatus != WatchlistAlertStatus.Active
            : watchlist.AlertStatus != WatchlistAlertStatus.Suspended &&
              watchlist.AlertStatus != WatchlistAlertStatus.Blocked;
        if (command.Enabled)
        {
            watchlist.ActivateAlerts(updatedAt);
        }
        else if (watchlist.AlertStatus != WatchlistAlertStatus.Blocked)
        {
            watchlist.SuspendAlerts(updatedAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MyAlertPreferencesResult(
            watchlist.AlertStatus,
            watchlist.BounceCount,
            changed);
    }
}
