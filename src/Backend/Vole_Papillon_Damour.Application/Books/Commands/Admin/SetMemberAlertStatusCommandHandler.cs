using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class SetMemberAlertStatusCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetMemberAlertStatusCommand, ErrorOr<AdminMemberOperationResult>>
{
    public async Task<ErrorOr<AdminMemberOperationResult>> Handle(
        SetMemberAlertStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MemberId is null || command.MemberId.Value == Guid.Empty)
        {
            return Errors.Book.MemberNotFound(Guid.Empty);
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var memberExists = await dbContext.Users.AnyAsync(
            user => user.Id == command.MemberId,
            cancellationToken);
        if (!memberExists)
        {
            return Errors.Book.MemberNotFound(command.MemberId.Value);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var watchlist = await dbContext.Watchlists.SingleOrDefaultAsync(
            candidate => candidate.Id == command.MemberId,
            cancellationToken);
        if (watchlist is null)
        {
            if (!command.Blocked)
            {
                return Errors.Watchlist.NotFound(command.MemberId.Value);
            }

            watchlist = Watchlist.Create(command.MemberId, now);
            dbContext.Watchlists.Add(watchlist);
        }

        var changed = command.Blocked
            ? watchlist.AlertStatus !=
              Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects.WatchlistAlertStatus.Blocked
            : watchlist.AlertStatus ==
              Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects.WatchlistAlertStatus.Blocked;
        if (command.Blocked)
        {
            watchlist.BlockAlerts(now);
        }
        else
        {
            watchlist.ActivateAlerts(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdminMemberOperationResult(
            command.MemberId.Value,
            watchlist.AlertStatus.ToString(),
            changed);
    }
}
