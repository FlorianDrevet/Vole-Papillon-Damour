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
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
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
                 movement.Type == BookMovementType.AnnouncementEntry ||
                 movement.Type == BookMovementType.ManualRequestHold))
            .OrderBy(movement => movement.OccurredAt)
            .ThenBy(movement => movement.Id)
            .ToListAsync(cancellationToken);

        // Tracked entities are loaded once, before the loop, and shared by every movement that needs them,
        // so in-memory corrections made by earlier iterations are seen by later ones (as before).
        var holdRequests = await LoadHoldRequestsAsync(movements, cancellationToken);
        var booksByIsbn = await LoadBooksAsync(movements, cancellationToken);
        var announcementsByKey = await LoadAnnouncementsAsync(session.Id, movements, cancellationToken);

        var reversedMovementCount = 0;
        foreach (var movement in movements)
        {
            if (movement.Type == BookMovementType.ManualRequestHold)
            {
                if (!holdRequests.TryGetValue(movement.Id, out var request) ||
                    request.Status != ManualBookRequestStatus.Found)
                {
                    continue;
                }

                if (movement.Quantity <= 0)
                {
                    return Error.Unexpected(
                        "Book.InvalidManualRequestHoldQuantity",
                        "A held manual request must have a positive quantity before cancellation.");
                }

                request.RevertScanHold(correctedAt);
                dbContext.BookMovements.Add(BookMovement.Create(
                    BookMovementId.CreateUnique(),
                    movement.Isbn13,
                    BookMovementType.ManualRequestHold,
                    -movement.Quantity,
                    correctedAt,
                    correctedAt,
                    clockSuspect: false,
                    session.Id,
                    command.UpdatedBy,
                    movement.AssoEventsId,
                    "Session.Cancel.ManualRequestHold.Reversal",
                    clientGestureId: null,
                    movement.Id));
                reversedMovementCount++;
                continue;
            }

            var book = ResolveBook(booksByIsbn, movement.Isbn13);
            if (book is null)
            {
                return Error.Unexpected(
                    "Book.MovementWithoutBook",
                    "The session movement points to a missing book.");
            }

            if (movement.Type == BookMovementType.AnnouncementEntry)
            {
                var announcement = announcementsByKey[(movement.Isbn13, movement.ClientGestureId)].SingleOrDefault();
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

    private async Task<Dictionary<BookMovementId, ManualBookRequest>> LoadHoldRequestsAsync(
        IReadOnlyCollection<BookMovement> movements,
        CancellationToken cancellationToken)
    {
        var holdMovementIds = movements
            .Where(movement => movement.Type == BookMovementType.ManualRequestHold)
            .Select(movement => movement.Id)
            .Distinct()
            .ToArray();
        if (holdMovementIds.Length == 0)
        {
            return new Dictionary<BookMovementId, ManualBookRequest>();
        }

        return await dbContext.ManualBookRequests
            .Where(candidate => candidate.HoldMovementId != null && holdMovementIds.Contains(candidate.HoldMovementId))
            .ToDictionaryAsync(candidate => candidate.HoldMovementId!, cancellationToken);
    }

    private async Task<Dictionary<Isbn13, Book>> LoadBooksAsync(
        IReadOnlyCollection<BookMovement> movements,
        CancellationToken cancellationToken)
    {
        var isbns = movements
            .Where(movement => movement.Type != BookMovementType.ManualRequestHold)
            .Select(movement => movement.Isbn13)
            .Distinct()
            .ToArray();
        var books = isbns.Length == 0
            ? new Dictionary<Isbn13, Book>()
            : await dbContext.Books
                .Where(book => isbns.Contains(book.Id))
                .ToDictionaryAsync(book => book.Id, cancellationToken);

        // A redirected book resolves to its canonical book: one more batch, only for those targets.
        var canonicalIsbns = books.Values
            .Where(book => book.RedirectedToIsbn13 is { } canonical && !books.ContainsKey(canonical))
            .Select(book => book.RedirectedToIsbn13!.Value)
            .Distinct()
            .ToArray();
        if (canonicalIsbns.Length > 0)
        {
            var canonicalBooks = await dbContext.Books
                .Where(book => canonicalIsbns.Contains(book.Id))
                .ToDictionaryAsync(book => book.Id, cancellationToken);
            foreach (var (isbn, canonicalBook) in canonicalBooks)
            {
                books[isbn] = canonicalBook;
            }
        }

        return books;
    }

    private async Task<ILookup<(Isbn13 Isbn13, Guid? ClientGestureId), BookAnnouncement>> LoadAnnouncementsAsync(
        ScanSessionId sessionId,
        IReadOnlyCollection<BookMovement> movements,
        CancellationToken cancellationToken)
    {
        var isbns = movements
            .Where(movement => movement.Type == BookMovementType.AnnouncementEntry)
            .Select(movement => movement.Isbn13)
            .Distinct()
            .ToArray();
        var announcements = isbns.Length == 0
            ? new List<BookAnnouncement>()
            : await dbContext.BookAnnouncements
                .Where(candidate =>
                    candidate.ScanSessionId == sessionId &&
                    isbns.Contains(candidate.Isbn13) &&
                    candidate.Status == BookAnnouncementStatus.Announced)
                .ToListAsync(cancellationToken);

        return announcements.ToLookup(announcement => (announcement.Isbn13, announcement.ClientGestureId));
    }

    private static Book? ResolveBook(IReadOnlyDictionary<Isbn13, Book> booksByIsbn, Isbn13 isbn13)
    {
        if (!booksByIsbn.TryGetValue(isbn13, out var book))
        {
            return null;
        }

        return book.RedirectedToIsbn13 is { } canonicalIsbn13
            ? booksByIsbn.GetValueOrDefault(canonicalIsbn13)
            : book;
    }
}
