using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using ScanSessionEntity = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;

namespace Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;

public sealed class ReassignSessionModeCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<ReassignSessionModeCommand, ErrorOr<ReassignSessionModeResult>>
{
    public async Task<ErrorOr<ReassignSessionModeResult>> Handle(
        ReassignSessionModeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null)
        {
            return Errors.Book.ScanSessionNotFound(Guid.Empty);
        }

        if (command.TargetMode == ScanMode.AvailableNow && command.TargetAssoEventsId is not null)
        {
            return Errors.Book.TargetFairOnlyForNextFair();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var correctedAt = dateTimeProvider.UtcNow;
        if (correctedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var session = await dbContext.ScanSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.ScanSessionId,
                cancellationToken);
        if (session is null)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        if (session.Status == ScanSessionStatus.Resumed)
        {
            return Errors.Book.ScanSessionAlreadyReassigned(command.ScanSessionId.Value);
        }

        if (session.Status != ScanSessionStatus.Completed)
        {
            return Errors.Book.ScanSessionMustBeClosed(command.ScanSessionId.Value);
        }

        if (session.Mode == command.TargetMode &&
            session.TargetAssoEventsId == command.TargetAssoEventsId)
        {
            return Errors.Book.ScanSessionAlreadyInTargetMode(command.ScanSessionId.Value);
        }

        if (command.TargetAssoEventsId is not null)
        {
            var targetFair = await dbContext.AssoEvents
                .SingleOrDefaultAsync(
                    assoEvent => assoEvent.Id == command.TargetAssoEventsId,
                    cancellationToken);
            if (targetFair is null)
            {
                return Errors.Book.FairNotFound(command.TargetAssoEventsId.Value);
            }

            if (targetFair.EventsType?.Value != EventsType.EventsTypeEnum.Books)
            {
                return Errors.Book.TargetFairMustBeBooks();
            }
        }

        var movements = await dbContext.BookMovements
            .Where(movement =>
                movement.ScanSessionId == session.Id &&
                movement.ReversalOfMovementId == null &&
                (movement.Type == BookMovementType.DirectEntry ||
                 movement.Type == BookMovementType.AnnouncementEntry))
            .OrderBy(movement => movement.OccurredAt)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        var reversedMovementCount = 0;
        var replayedMovementCount = 0;
        foreach (var movement in movements)
        {
            var book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == movement.Isbn13, cancellationToken);
            if (book is null)
            {
                return Error.Unexpected(
                    "Book.MovementWithoutBook",
                    "The session movement points to a missing book.");
            }

            var reverseResult = await ReverseMovementAsync(
                session,
                movement,
                book,
                command.UpdatedBy,
                correctedAt,
                cancellationToken);
            if (reverseResult.IsError)
            {
                return reverseResult.Errors;
            }

            reversedMovementCount++;
            dbContext.BookMovements.Add(reverseResult.Value);

            var replayResult = ReplayMovement(
                session,
                movement,
                book,
                command,
                correctedAt);
            if (replayResult is not null)
            {
                replayedMovementCount++;
                dbContext.BookMovements.Add(replayResult.Value.Movement);
                if (replayResult.Value.Announcement is not null)
                {
                    dbContext.BookAnnouncements.Add(replayResult.Value.Announcement);
                }
            }
        }

        if (!session.Reassign(command.TargetMode, command.TargetAssoEventsId))
        {
            return Errors.Book.ScanSessionAlreadyReassigned(command.ScanSessionId.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var cancelledAlertCount = await bookAlertOutbox.CancelPendingForSessionAsync(
            session.Id,
            cancellationToken);
        if (cancelledAlertCount > 0)
        {
            await bookAlertOutbox.QueueForSessionAsync(
                session.Id,
                correctedAt,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReassignSessionModeResult(
            session.Id,
            session.Mode,
            session.TargetAssoEventsId,
            reversedMovementCount,
            replayedMovementCount);
    }

    private async Task<ErrorOr<BookMovement>> ReverseMovementAsync(
        ScanSessionEntity session,
        BookMovement movement,
        Book book,
        Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId updatedBy,
        DateTime correctedAt,
        CancellationToken cancellationToken)
    {
        if (movement.Quantity <= 0)
        {
            return Error.Unexpected(
                "Book.InvalidSessionMovementQuantity",
                "A kept scan must have a positive quantity before reversal.");
        }

        if (movement.Type == BookMovementType.DirectEntry)
        {
            book.ApplyQuantityCorrection(
                Math.Max(0, book.QuantityAvailable - movement.Quantity),
                correctedAt);
        }
        else
        {
            var announcements = await dbContext.BookAnnouncements
                .Where(announcement =>
                    announcement.ScanSessionId == session.Id &&
                    announcement.Isbn13 == movement.Isbn13 &&
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    announcement.ClientGestureId == movement.ClientGestureId)
                .ToListAsync(cancellationToken);
            if (announcements.Count != 1 || !announcements[0].Cancel())
            {
                return Errors.Book.AnnouncementAlreadyReleased(movement.Isbn13.Value);
            }

            book.RecordAnnouncementEntry(correctedAt);
        }

        return BookMovement.Create(
            BookMovementId.CreateUnique(),
            movement.Isbn13,
            BookMovementType.Correction,
            -movement.Quantity,
            correctedAt,
            correctedAt,
            clockSuspect: false,
            session.Id,
            updatedBy,
            movement.AssoEventsId,
            "Session.Reassign.Reversal",
            clientGestureId: null,
            movement.Id);
    }

    private static (BookMovement Movement, BookAnnouncement? Announcement)? ReplayMovement(
        ScanSessionEntity session,
        BookMovement originalMovement,
        Book book,
        ReassignSessionModeCommand command,
        DateTime correctedAt)
    {
        if (command.TargetMode == ScanMode.AvailableNow)
        {
            book.RecordAvailableEntry(correctedAt);
            return (
                BookMovement.Create(
                    BookMovementId.CreateUnique(),
                    originalMovement.Isbn13,
                    BookMovementType.DirectEntry,
                    originalMovement.Quantity,
                    correctedAt,
                    correctedAt,
                    clockSuspect: false,
                    session.Id,
                    command.UpdatedBy,
                    assoEventsId: null,
                    "Session.Reassign.Replay",
                    clientGestureId: null),
                Announcement: null);
        }

        book.RecordAnnouncementEntry(correctedAt);
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            originalMovement.Isbn13,
            command.TargetAssoEventsId,
            originalMovement.Quantity,
            correctedAt,
            session.Id);
        return (
            BookMovement.Create(
                BookMovementId.CreateUnique(),
                originalMovement.Isbn13,
                BookMovementType.AnnouncementEntry,
                originalMovement.Quantity,
                correctedAt,
                correctedAt,
                clockSuspect: false,
                session.Id,
                command.UpdatedBy,
                command.TargetAssoEventsId,
                "Session.Reassign.Replay",
                clientGestureId: null),
            announcement);
    }
}
