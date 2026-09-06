using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class CancelScanSessionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<CancelScanSessionCommand, ErrorOr<AdminScanSessionOperationResult>>
{
    public async Task<ErrorOr<AdminScanSessionOperationResult>> Handle(
        CancelScanSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null)
        {
            return Errors.Book.ScanSessionNotFound(Guid.Empty);
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
        var session = await dbContext.ScanSessions.SingleOrDefaultAsync(
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

        var movements = await dbContext.BookMovements
            .Where(movement =>
                movement.ScanSessionId == session.Id &&
                !dbContext.BookMovements.Any(reversal => reversal.ReversalOfMovementId == movement.Id) &&
                (movement.Type == BookMovementType.DirectEntry ||
                 movement.Type == BookMovementType.AnnouncementEntry))
            .OrderBy(movement => movement.OccurredAt)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        var reversedMovementCount = 0;
        foreach (var movement in movements)
        {
            var book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == movement.Isbn13,
                cancellationToken);
            if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
            {
                book = await dbContext.Books.SingleOrDefaultAsync(
                    candidate => candidate.Id == canonicalIsbn13,
                    cancellationToken);
            }

            if (book is null)
            {
                return Error.Unexpected(
                    "Book.MovementWithoutBook",
                    "The session movement points to a missing book.");
            }

            if (movement.Type == BookMovementType.AnnouncementEntry)
            {
                var announcement = await dbContext.BookAnnouncements.SingleOrDefaultAsync(
                    candidate =>
                        candidate.ScanSessionId == session.Id &&
                        candidate.Isbn13 == movement.Isbn13 &&
                        candidate.ClientGestureId == movement.ClientGestureId &&
                        candidate.Status == BookAnnouncementStatus.Announced,
                    cancellationToken);
                if (announcement is null)
                {
                    return Errors.Book.AnnouncementAlreadyReleased(movement.Isbn13.Value);
                }

                announcement.Cancel();
                book.RecordAnnouncementEntry(correctedAt);
            }
            else
            {
                if (movement.Quantity <= 0)
                {
                    return Error.Unexpected(
                        "Book.InvalidSessionMovementQuantity",
                        "A kept scan must have a positive quantity before cancellation.");
                }

                if (movement.Quantity > book.QuantityAvailable)
                {
                    return Errors.Book.SessionMovementAlreadyConsumed(movement.Id.Value);
                }

                book.ApplyQuantityCorrection(
                    book.QuantityAvailable - movement.Quantity,
                    correctedAt);
                dbContext.BookMovements.Add(BookMovement.Create(
                    BookMovementId.CreateUnique(),
                    book.Id,
                    BookMovementType.Correction,
                    -movement.Quantity,
                    correctedAt,
                    correctedAt,
                    clockSuspect: false,
                    session.Id,
                    command.UpdatedBy,
                    movement.AssoEventsId,
                    "Session.Cancel.Reversal",
                    clientGestureId: null,
                    movement.Id));
            }

            if (movement.Type == BookMovementType.AnnouncementEntry)
            {
                dbContext.BookMovements.Add(BookMovement.Create(
                    BookMovementId.CreateUnique(),
                    movement.Isbn13,
                    BookMovementType.Correction,
                    -movement.Quantity,
                    correctedAt,
                    correctedAt,
                    clockSuspect: false,
                    session.Id,
                    command.UpdatedBy,
                    movement.AssoEventsId,
                    "Announcement.Correction: Session.Cancel.Reversal",
                    clientGestureId: null,
                    movement.Id));
            }

            reversedMovementCount++;
        }

        if (session.Status == ScanSessionStatus.InProgress)
        {
            session.Close(ScanCloseReason.Manual, correctedAt);
        }

        session.MarkResumedAfterCorrection();
        await dbContext.SaveChangesAsync(cancellationToken);
        var cancelledAlertCount = await bookAlertOutbox.CancelPendingForSessionAsync(
            session.Id,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminScanSessionOperationResult(
            session.Id.Value,
            reversedMovementCount,
            cancelledAlertCount,
            Changed: reversedMovementCount > 0 || cancelledAlertCount > 0);
    }
}
