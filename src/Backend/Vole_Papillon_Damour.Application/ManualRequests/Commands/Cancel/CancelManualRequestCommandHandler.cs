using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Cancel;

public sealed class CancelManualRequestCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox alertOutbox)
    : IRequestHandler<CancelManualRequestCommand, ErrorOr<ManualRequestReleaseResult>>
{
    public async Task<ErrorOr<ManualRequestReleaseResult>> Handle(
        CancelManualRequestCommand command,
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

        if (!request.IsOpen)
        {
            return Errors.ManualRequest.InvalidTransition();
        }

        BookMovementId? releaseMovementId = null;
        if (request.Status == ManualBookRequestStatus.Found)
        {
            var release = await ManualRequestRelease.ReleaseAsync(
                dbContext, request, command.By, now, cancellationToken);
            if (release.IsError)
            {
                return release.Errors;
            }

            releaseMovementId = release.Value;
        }

        request.Cancel(command.By, releaseMovementId, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        var notifiedMembers = releaseMovementId is { } movementId
            ? await alertOutbox.QueueForReleaseAsync(movementId, now, cancellationToken)
            : 0;
        if (releaseMovementId is not null)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ManualRequestReleaseResult(notifiedMembers);
    }
}
