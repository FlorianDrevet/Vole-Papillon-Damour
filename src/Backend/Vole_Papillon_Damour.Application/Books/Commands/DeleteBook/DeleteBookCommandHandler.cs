using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.DeleteBook;

public sealed class DeleteBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<DeleteBookCommand, ErrorOr<DeleteBookResult>>
{
    public async Task<ErrorOr<DeleteBookResult>> Handle(
        DeleteBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.DeletedBy is null || command.DeletedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidDeletedBy", "A deleting user identifier is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var hasSale = await dbContext.BookMovements
            .AnyAsync(
                movement => movement.Isbn13 == isbn13 &&
                            movement.Type == BookMovementType.Sale,
                cancellationToken);
        if (hasSale)
        {
            return Errors.Book.BookHasSales(isbn13.Value);
        }

        var hasAnnouncements = await dbContext.BookAnnouncements
            .AnyAsync(announcement => announcement.Isbn13 == isbn13, cancellationToken);
        if (hasAnnouncements)
        {
            return Errors.Book.BookHasHistory(isbn13.Value);
        }

        var hasMovements = await dbContext.BookMovements
            .AnyAsync(movement => movement.Isbn13 == isbn13, cancellationToken);
        var isIncompleteCandidate =
            book.MetadataStatus is BookMetadataStatus.Pending or BookMetadataStatus.Failed or BookMetadataStatus.NotFound ||
            string.IsNullOrWhiteSpace(book.Title);

        if (hasMovements && isIncompleteCandidate)
        {
            var discardedAt = dateTimeProvider.UtcNow;
            if (discardedAt.Kind != DateTimeKind.Utc)
            {
                return Errors.Book.InvalidScanTimestamp();
            }

            if (book.QuantityAvailable > 0)
            {
                var withdrawal = await BookWithdrawal.WithdrawAsync(
                    dbContext,
                    isbn13.Value,
                    book.QuantityAvailable,
                    "Retrait d'un ISBN abandonné dans la file d'analyse",
                    command.DeletedBy,
                    discardedAt,
                    cancellationToken);
                if (withdrawal.IsError)
                {
                    return withdrawal.Errors;
                }
            }

            book.DiscardMetadataCandidate(discardedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new DeleteBookResult(isbn13.Value, Deleted: true);
        }

        if (hasMovements)
        {
            return Errors.Book.BookHasHistory(isbn13.Value);
        }

        dbContext.Books.Remove(book);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeleteBookResult(isbn13.Value, Deleted: true);
    }
}
