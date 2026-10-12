using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.DeleteRequester;

public sealed class DeleteManualRequesterCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox alertOutbox)
    : IRequestHandler<DeleteManualRequesterCommand, ErrorOr<ManualRequesterDeletionResult>>
{
    public async Task<ErrorOr<ManualRequesterDeletionResult>> Handle(
        DeleteManualRequesterCommand command,
        CancellationToken cancellationToken)
    {
        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The requester timestamp must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var requester = await dbContext.ManualRequesters
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequesterId, cancellationToken);
        if (requester is null)
        {
            return Errors.ManualRequest.RequesterNotFound(command.RequesterId);
        }

        var openRequests = await dbContext.ManualBookRequests
            .Where(request => request.RequesterId == requester.Id &&
                              (request.Status == ManualBookRequestStatus.Searching ||
                               request.Status == ManualBookRequestStatus.Found))
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(cancellationToken);
        var releaseMovementIds = new List<BookMovementId>();

        foreach (var request in openRequests)
        {
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
                releaseMovementIds.Add(release.Value);
            }

            request.Cancel(command.By, releaseMovementId, now);
        }

        requester.Erase(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        var notifiedMemberCount = 0;
        foreach (var movementId in releaseMovementIds)
        {
            notifiedMemberCount += await alertOutbox.QueueForReleaseAsync(
                movementId,
                now,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ManualRequesterDeletionResult(openRequests.Count, notifiedMemberCount);
    }
}
