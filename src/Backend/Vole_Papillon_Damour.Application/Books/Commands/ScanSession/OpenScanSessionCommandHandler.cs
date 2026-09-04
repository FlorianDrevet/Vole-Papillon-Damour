using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using ScanSessionAggregate = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed class OpenScanSessionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<OpenScanSessionCommand, ErrorOr<ScanSessionResult>>
{
    public async Task<ErrorOr<ScanSessionResult>> Handle(
        OpenScanSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(command.Mode))
        {
            return Errors.Book.InvalidScanMode();
        }

        if (command.Mode == ScanMode.AvailableNow && command.TargetAssoEventsId is not null)
        {
            return Errors.Book.TargetFairOnlyForNextFair();
        }

        if (command.ClientSessionId == Guid.Empty)
        {
            return Error.Validation(
                "Book.InvalidClientSessionId",
                "The client session identifier cannot be empty.");
        }

        var startedAt = dateTimeProvider.UtcNow;
        if (startedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (command.ClientSessionId is { } requestedSessionId)
        {
            var existingClientSession = await dbContext.ScanSessions
                .SingleOrDefaultAsync(
                    session => session.Id == ScanSessionId.Create(requestedSessionId),
                    cancellationToken);
            if (existingClientSession is not null)
            {
                if (existingClientSession.VolunteerId != command.VolunteerId ||
                    existingClientSession.Mode != command.Mode ||
                    existingClientSession.TargetAssoEventsId != command.TargetAssoEventsId)
                {
                    return Error.Conflict(
                        "Book.ClientSessionConflict",
                        "The client session identifier is already assigned to another session.");
                }

                await transaction.CommitAsync(cancellationToken);
                return ScanSessionResult.From(existingClientSession);
            }
        }

        if (command.TargetAssoEventsId is { } targetFairId)
        {
            var targetFair = await dbContext.AssoEvents
                .SingleOrDefaultAsync(
                    assoEvent => assoEvent.Id == targetFairId,
                    cancellationToken);
            if (targetFair is null)
            {
                return Errors.Book.FairNotFound(targetFairId.Value);
            }

            if (targetFair.IsCancelled)
            {
                return Errors.Book.FairCancelled(targetFairId.Value);
            }

            if (targetFair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
            {
                return Errors.Book.TargetFairMustBeBooks();
            }
        }

        var existingSession = await dbContext.ScanSessions
            .SingleOrDefaultAsync(
                session =>
                    session.VolunteerId == command.VolunteerId &&
                    session.Status == ScanSessionStatus.InProgress,
                cancellationToken);

        if (existingSession is not null)
        {
            return Errors.Book.ActiveScanSessionExists(command.VolunteerId);
        }

        var session = command.ClientSessionId is { } clientSessionId
            ? ScanSessionAggregate.Create(
                ScanSessionId.Create(clientSessionId),
                command.VolunteerId,
                command.Mode,
                command.TargetAssoEventsId,
                startedAt)
            : ScanSessionAggregate.Create(
                command.VolunteerId,
                command.Mode,
                command.TargetAssoEventsId,
                startedAt);
        dbContext.ScanSessions.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ScanSessionResult.From(session);
    }
}
