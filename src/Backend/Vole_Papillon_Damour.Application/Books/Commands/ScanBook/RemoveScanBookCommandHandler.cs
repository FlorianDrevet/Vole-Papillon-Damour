using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed class RemoveScanBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<RemoveScanBookCommand, ErrorOr<ScanBookRemovalResult>>
{
    private const string ScanRemovalReversalNote = "Session.Scan.Remove.Reversal";
    private const string AnnouncementReversalNote = "Announcement.Correction: Session.Scan.Remove.Reversal";
    private const string ManualRequestHoldReversalNote = "Session.Scan.Remove.ManualRequestHold.Reversal";

    public async Task<ErrorOr<ScanBookRemovalResult>> Handle(
        RemoveScanBookCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null || command.VolunteerId is null ||
            command.VolunteerId.Value == Guid.Empty || command.ClientGestureId == Guid.Empty)
        {
            return Error.Validation(
                "Book.InvalidScanRemoval",
                "A valid scan session, scan gesture, and volunteer are required.");
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
        if (session is null || session.VolunteerId != command.VolunteerId)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        var movement = await dbContext.BookMovements.SingleOrDefaultAsync(
            candidate => candidate.ScanSessionId == session.Id &&
                         candidate.ClientGestureId == command.ClientGestureId &&
                         candidate.ReversalOfMovementId == null &&
                         (candidate.Type == BookMovementType.DirectEntry ||
                          candidate.Type == BookMovementType.AnnouncementEntry ||
                          candidate.Type == BookMovementType.ManualRequestHold ||
                          candidate.Type == BookMovementType.Rejection),
            cancellationToken);
        if (movement is null)
        {
            return Error.NotFound(
                "Book.ScanSessionMovementNotFound",
                "The scan was not found in this session or cannot be removed.");
        }

        var existingReversal = await dbContext.BookMovements.SingleOrDefaultAsync(
            candidate => candidate.ReversalOfMovementId == movement.Id,
            cancellationToken);
        if (existingReversal is not null)
        {
            var currentBook = await GetBookAsync(movement.Isbn13, cancellationToken);
            if (currentBook is null)
            {
                return Error.Unexpected("Book.MovementWithoutBook", "The session movement points to a missing book.");
            }

            return new ScanBookRemovalResult(
                movement.Isbn13.Value,
                currentBook.QuantityAvailable,
                await GetQuantityAnnouncedAsync(movement.Isbn13, cancellationToken),
                session.Id,
                AlreadyRemoved: true);
        }

        var book = await GetBookAsync(movement.Isbn13, cancellationToken);
        if (book is null)
        {
            return Error.Unexpected("Book.MovementWithoutBook", "The session movement points to a missing book.");
        }

        var kept = movement.Type != BookMovementType.Rejection;
        switch (movement.Type)
        {
            case BookMovementType.ManualRequestHold:
            {
                var request = await dbContext.ManualBookRequests.SingleOrDefaultAsync(
                    candidate => candidate.HoldMovementId == movement.Id,
                    cancellationToken);
                if (request is null || request.Status != ManualBookRequestStatus.Found)
                {
                    return Errors.Book.SessionMovementAlreadyConsumed(movement.Id.Value);
                }

                if (movement.Quantity <= 0)
                {
                    return Error.Unexpected(
                        "Book.InvalidManualRequestHoldQuantity",
                        "A held manual request must have a positive quantity before removal.");
                }

                request.RevertScanHold(correctedAt);
                AddReversal(movement, session.Id, command.VolunteerId, -movement.Quantity,
                    BookMovementType.ManualRequestHold, ManualRequestHoldReversalNote, correctedAt);
                break;
            }
            case BookMovementType.AnnouncementEntry:
            {
                var announcement = await dbContext.BookAnnouncements.SingleOrDefaultAsync(
                    candidate => candidate.ScanSessionId == session.Id &&
                                 candidate.Isbn13 == movement.Isbn13 &&
                                 candidate.ClientGestureId == movement.ClientGestureId &&
                                 candidate.Status == BookAnnouncementStatus.Announced,
                    cancellationToken);
                if (announcement is null)
                {
                    return Errors.Book.AnnouncementAlreadyReleased(movement.Isbn13.Value);
                }

                if (!announcement.Cancel())
                {
                    return Errors.Book.AnnouncementAlreadyReleased(movement.Isbn13.Value);
                }

                book.RecordAnnouncementEntry(correctedAt);
                AddReversal(movement, session.Id, command.VolunteerId, -movement.Quantity,
                    BookMovementType.Correction, AnnouncementReversalNote, correctedAt);
                break;
            }
            case BookMovementType.DirectEntry:
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
                AddReversal(movement, session.Id, command.VolunteerId, -movement.Quantity,
                    BookMovementType.Correction, ScanRemovalReversalNote, correctedAt);
                break;
            }
            case BookMovementType.Rejection:
            {
                if (movement.Quantity <= 0)
                {
                    return Error.Unexpected(
                        "Book.InvalidScanMovementQuantity",
                        "A rejected scan must have a positive quantity before removal.");
                }

                book.ReverseRejection(correctedAt);
                AddReversal(movement, session.Id, command.VolunteerId, -movement.Quantity,
                    BookMovementType.Correction, ScanRemovalReversalNote, correctedAt);
                break;
            }
        }

        if (!session.RemoveScan(kept, correctedAt))
        {
            return Error.Unexpected(
                "Book.ScanSessionCountMismatch",
                "The scan cannot be removed because the session counts are inconsistent.");
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

        return new ScanBookRemovalResult(
            movement.Isbn13.Value,
            book.QuantityAvailable,
            await GetQuantityAnnouncedAsync(movement.Isbn13, cancellationToken),
            session.Id,
            AlreadyRemoved: false);
    }

    private async Task<Book?> GetBookAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == isbn13,
            cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == canonicalIsbn13,
                cancellationToken);
        }

        return book;
    }

    private async Task<int> GetQuantityAnnouncedAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        return await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == isbn13 &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
    }

    private void AddReversal(
        BookMovement movement,
        ScanSessionId scanSessionId,
        UserId updatedBy,
        int quantity,
        BookMovementType type,
        string note,
        DateTime occurredAt)
    {
        dbContext.BookMovements.Add(BookMovement.Create(
            BookMovementId.CreateUnique(),
            movement.Isbn13,
            type,
            quantity,
            occurredAt,
            occurredAt,
            clockSuspect: false,
            scanSessionId,
            updatedBy,
            movement.AssoEventsId,
            note,
            clientGestureId: null,
            movement.Id));
    }
}
