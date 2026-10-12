using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;

public sealed class RefuseManualRequestCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox alertOutbox)
    : IRequestHandler<RefuseManualRequestCommand, ErrorOr<ManualRequestReleaseResult>>
{
    public async Task<ErrorOr<ManualRequestReleaseResult>> Handle(
        RefuseManualRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The request timestamp must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.ManualBookRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequestId, cancellationToken);
        if (request is null)
        {
            return Errors.ManualRequest.RequestNotFound(command.RequestId);
        }

        if (request.Status != ManualBookRequestStatus.Found)
        {
            return Errors.ManualRequest.InvalidTransition();
        }

        if (command.KeepSearching)
        {
            if (request.HoldMovementId is not { } holdMovementId)
            {
                return Errors.ManualRequest.InvalidTransition();
            }

            request.Refuse(command.By, keepSearching: true, holdMovementId, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ManualRequestReleaseResult(NotifiedMemberCount: 0);
        }

        var release = await ManualRequestRelease.ReleaseAsync(
            dbContext, request, command.By, now, cancellationToken);
        if (release.IsError)
        {
            return release.Errors;
        }

        request.Refuse(command.By, keepSearching: false, release.Value, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        var notifiedMembers = await alertOutbox.QueueForReleaseAsync(release.Value, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualRequestReleaseResult(notifiedMembers);
    }
}
