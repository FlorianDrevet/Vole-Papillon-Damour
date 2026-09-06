using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class AddBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddBookCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        AddBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.QuantityAvailable < 0 || command.QuantityAvailable > 100_000)
        {
            return Errors.Book.InvalidCorrectionQuantity();
        }

        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Trim().Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (command.AddedBy is null || command.AddedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidAddedBy", "An adding user identifier is required.");
        }

        var addedAt = dateTimeProvider.UtcNow;
        if (addedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var fields = command.Fields?.Distinct().ToArray() ?? [];
        if (fields.Any(field => !Enum.IsDefined(field)))
        {
            return Errors.Book.InvalidMetadataFields();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        if (await dbContext.Books.AnyAsync(book => book.Id == isbn13, cancellationToken))
        {
            return Errors.Book.BookAlreadyExists(isbn13.Value);
        }

        var book = Book.Create(isbn13, addedAt);
        if (fields.Length > 0)
        {
            try
            {
                book.ApplyManualMetadata(
                    new BookMetadataPatch(
                        command.Title,
                        command.Authors,
                        command.Publisher,
                        command.PublicationYear,
                        command.PhysicalFormat,
                        command.Language,
                        command.Genre,
                        command.CoverUrl,
                        fields,
                        command.WorkId),
                    addedAt);
            }
            catch (ArgumentException)
            {
                return Errors.Book.InvalidMetadataValues();
            }
        }

        dbContext.Books.Add(book);
        Guid? movementId = null;
        if (command.QuantityAvailable > 0)
        {
            book.RecordAvailableEntry(addedAt, command.QuantityAvailable);
            var movement = BookMovement.Create(
                BookMovementId.CreateUnique(),
                isbn13,
                BookMovementType.DirectEntry,
                command.QuantityAvailable,
                addedAt,
                addedAt,
                clockSuspect: false,
                scanSessionId: null,
                command.AddedBy,
                assoEventsId: null,
                command.Note.Trim(),
                clientGestureId: null);
            movementId = movement.Id.Value;
            dbContext.BookMovements.Add(movement);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminBookOperationResult(
            isbn13.Value,
            book.QuantityAvailable,
            QuantityAnnounced: 0,
            Changed: true,
            movementId);
    }
}
