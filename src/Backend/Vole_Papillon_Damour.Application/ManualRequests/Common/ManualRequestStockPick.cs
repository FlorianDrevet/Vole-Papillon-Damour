using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

internal static class ManualRequestStockPick
{
    public static async Task<ErrorOr<Success>> PickAsync(
        IProjectDbContext db,
        ManualBookRequest request,
        UserId by,
        DateTime now,
        CancellationToken ct)
    {
        if (request.Status != ManualBookRequestStatus.Searching)
        {
            return Errors.ManualRequest.InvalidTransition();
        }

        if (by is null || by.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The request timestamp must be expressed in UTC.");
        }

        var availableBook = await FindAvailableBookAsync(db, request, ct);
        if (availableBook is not null)
        {
            var withdrawal = await BookWithdrawal.WithdrawAsync(
                db,
                availableBook.Id.Value,
                quantity: 1,
                note: "ManualRequest.StockPick",
                by,
                now,
                ct);
            if (withdrawal.IsError)
            {
                return withdrawal.Errors;
            }

            request.MarkFoundFromStock(
                withdrawal.Value.Isbn13,
                pickedFromFairId: null,
                pickedAnnouncementSessionId: null,
                withdrawal.Value.MovementId,
                by,
                now);
            return Result.Success;
        }

        var announcement = await FindLatestAnnouncementAsync(db, request, ct);
        if (announcement is null || announcement.AssoEventsId is not { } fairId)
        {
            return Errors.ManualRequest.NothingInStock();
        }

        if (announcement.Quantity > 1)
        {
            announcement.ApplyQuantityCorrection(announcement.Quantity - 1);
        }
        else
        {
            announcement.Cancel();
        }

        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            announcement.Isbn13,
            BookMovementType.Correction,
            quantity: -1,
            occurredAt: now,
            receivedAt: now,
            clockSuspect: false,
            scanSessionId: null,
            volunteerId: by,
            assoEventsId: fairId,
            note: "Announcement.Correction: ManualRequest.StockPick",
            clientGestureId: null);
        db.BookMovements.Add(movement);
        request.MarkFoundFromStock(
            announcement.Isbn13,
            fairId,
            announcement.ScanSessionId,
            movement.Id,
            by,
            now);

        return Result.Success;
    }

    private static async Task<Book?> FindAvailableBookAsync(
        IProjectDbContext db,
        ManualBookRequest request,
        CancellationToken ct)
    {
        if (request.Scope == ManualBookRequestScope.Work)
        {
            return await db.Books
                .Where(book => book.RedirectedToIsbn13 == null &&
                               book.WorkId == request.WorkId &&
                               book.QuantityAvailable > 0)
                .OrderByDescending(book => book.QuantityAvailable)
                .ThenBy(book => book.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (request.Isbn13 is not { } requestedIsbn)
        {
            return null;
        }

        var currentIsbn = requestedIsbn;
        var visited = new HashSet<Isbn13>();
        while (visited.Add(currentIsbn))
        {
            var currentBook = await db.Books
                .AsNoTracking()
                .SingleOrDefaultAsync(book => book.Id == currentIsbn, ct);
            if (currentBook is null)
            {
                return null;
            }

            if (currentBook.RedirectedToIsbn13 is not { } redirectedIsbn)
            {
                return await db.Books.SingleOrDefaultAsync(book =>
                    book.Id == currentIsbn && book.RedirectedToIsbn13 == null && book.QuantityAvailable > 0, ct);
            }

            currentIsbn = redirectedIsbn;
        }

        return null;
    }

    private static async Task<BookAnnouncement?> FindLatestAnnouncementAsync(
        IProjectDbContext db,
        ManualBookRequest request,
        CancellationToken ct)
    {
        var announcements = db.BookAnnouncements
            .Where(announcement => announcement.Status == BookAnnouncementStatus.Announced &&
                                   announcement.AssoEventsId != null);

        if (request.Scope == ManualBookRequestScope.Edition && request.Isbn13 is { } isbn13)
        {
            announcements = announcements.Where(announcement => announcement.Isbn13 == isbn13);
        }
        else if (request.Scope == ManualBookRequestScope.Work && request.WorkId is { } workId)
        {
            announcements = announcements.Where(announcement => db.Books.Any(book =>
                book.Id == announcement.Isbn13 && book.WorkId == workId));
        }
        else
        {
            return null;
        }

        return await announcements
            .OrderByDescending(announcement => announcement.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
