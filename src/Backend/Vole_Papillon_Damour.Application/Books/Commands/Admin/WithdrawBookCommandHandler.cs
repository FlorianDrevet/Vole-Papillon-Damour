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

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class WithdrawBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<WithdrawBookCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        WithdrawBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.Quantity <= 0)
        {
            return Errors.Book.InvalidWithdrawalQuantity();
        }

        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Trim().Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == isbn13,
            cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == isbn13,
                cancellationToken);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        if (command.Quantity > book.QuantityAvailable)
        {
            return Errors.Book.InvalidWithdrawalQuantity();
        }

        book.ApplyQuantityCorrection(book.QuantityAvailable - command.Quantity, updatedAt);
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Withdrawal,
            -command.Quantity,
            updatedAt,
            updatedAt,
            clockSuspect: false,
            scanSessionId: null,
            command.UpdatedBy,
            assoEventsId: null,
            command.Note.Trim(),
            clientGestureId: null);
        dbContext.BookMovements.Add(movement);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var announcedQuantity = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == isbn13 &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
        return new AdminBookOperationResult(
            isbn13.Value,
            book.QuantityAvailable,
            announcedQuantity,
            Changed: true,
            movement.Id.Value);
    }
}
