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

namespace Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;

public sealed class AdjustQuantityCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AdjustQuantityCommand, ErrorOr<AdjustQuantityResult>>
{
    public async Task<ErrorOr<AdjustQuantityResult>> Handle(
        AdjustQuantityCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.QuantityAvailable < 0)
        {
            return Errors.Book.InvalidCorrectionQuantity();
        }

        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (command.VolunteerId is null || command.VolunteerId.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidVolunteerId", "A volunteer identifier is required.");
        }

        var correctedAt = dateTimeProvider.UtcNow;
        if (correctedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var previousQuantityAvailable = book.QuantityAvailable;
        var delta = book.ApplyQuantityCorrection(command.QuantityAvailable, correctedAt);
        if (delta == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new AdjustQuantityResult(
                isbn13.Value,
                previousQuantityAvailable,
                book.QuantityAvailable,
                delta,
                MovementId: null,
                Changed: false);
        }

        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Correction,
            delta,
            correctedAt,
            correctedAt,
            clockSuspect: false,
            scanSessionId: null,
            command.VolunteerId,
            assoEventsId: null,
            command.Note,
            clientGestureId: null);
        dbContext.BookMovements.Add(movement);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdjustQuantityResult(
            isbn13.Value,
            previousQuantityAvailable,
            book.QuantityAvailable,
            delta,
            movement.Id,
            Changed: true);
    }
}
