using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed record BookWithdrawalOutcome(
    Isbn13 Isbn13,
    int QuantityWithdrawn,
    int QuantityAvailable,
    BookMovementId MovementId);

public static class BookWithdrawal
{
    public static async Task<ErrorOr<BookWithdrawalOutcome>> WithdrawAsync(
        IProjectDbContext db,
        string isbn,
        int quantity,
        string note,
        UserId by,
        DateTime at,
        CancellationToken ct)
    {
        if (!Isbn13.TryCreate(isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(isbn);
        }

        if (quantity <= 0)
        {
            return Errors.Book.InvalidWithdrawalQuantity();
        }

        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (by is null || by.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        if (at.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var book = await db.Books.SingleOrDefaultAsync(candidate => candidate.Id == isbn13, ct);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await db.Books.SingleOrDefaultAsync(candidate => candidate.Id == isbn13, ct);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        if (quantity > book.QuantityAvailable)
        {
            return Errors.Book.InvalidWithdrawalQuantity();
        }

        book.ApplyQuantityCorrection(book.QuantityAvailable - quantity, at);
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Withdrawal,
            -quantity,
            at,
            at,
            clockSuspect: false,
            scanSessionId: null,
            by,
            assoEventsId: null,
            note.Trim(),
            clientGestureId: null);
        db.BookMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        return new BookWithdrawalOutcome(isbn13, quantity, book.QuantityAvailable, movement.Id);
    }
}
