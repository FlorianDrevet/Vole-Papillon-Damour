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

public sealed class RemoveScanSessionMovementCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<RemoveScanSessionMovementCommand, ErrorOr<AdminScanSessionOperationResult>>
{
    public async Task<ErrorOr<AdminScanSessionOperationResult>> Handle(
        RemoveScanSessionMovementCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null)
        {
            return Errors.Book.ScanSessionNotFound(Guid.Empty);
        }

        if (command.MovementId == Guid.Empty || command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidSessionCorrection", "A valid movement and administrator are required.");
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

        var movement = await dbContext.BookMovements.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == BookMovementId.Create(command.MovementId) &&
                candidate.ScanSessionId == session.Id &&
                !dbContext.BookMovements.Any(reversal => reversal.ReversalOfMovementId == candidate.Id) &&
                (candidate.Type == BookMovementType.DirectEntry ||
                 candidate.Type == BookMovementType.AnnouncementEntry),
            cancellationToken);
        if (movement is null)
        {
            return Error.NotFound("Book.SessionMovementNotFound", "The session movement was not found or is already corrected.");
        }

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
            return Error.Unexpected("Book.MovementWithoutBook", "The session movement points to a missing book.");
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
                    "A kept scan must have a positive quantity before removal.");
            }

            if (movement.Quantity > book.QuantityAvailable)
            {
                return Errors.Book.SessionMovementAlreadyConsumed(movement.Id.Value);
            }

            book.ApplyQuantityCorrection(book.QuantityAvailable - movement.Quantity, correctedAt);
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
                "Session.Movement.Remove.Reversal",
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
                "Announcement.Correction: Session.Movement.Remove.Reversal",
                clientGestureId: null,
                movement.Id));
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
        if (cancelledAlertCount > 0)
        {
            await bookAlertOutbox.QueueForSessionAsync(
                session.Id,
                correctedAt,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AdminScanSessionOperationResult(
            session.Id.Value,
            1,
            cancelledAlertCount,
            Changed: true);
    }
}
