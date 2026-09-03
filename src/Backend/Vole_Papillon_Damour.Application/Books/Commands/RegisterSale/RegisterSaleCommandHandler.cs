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

namespace Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;

public sealed class RegisterSaleCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterSaleCommand, ErrorOr<RegisterSaleResult>>
{
    public async Task<ErrorOr<RegisterSaleResult>> Handle(
        RegisterSaleCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.Quantity <= 0)
        {
            return Errors.Book.InvalidSaleQuantity();
        }

        if (command.VolunteerId is null || command.VolunteerId.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidVolunteerId", "A volunteer identifier is required.");
        }

        if (command.ClientGestureId == Guid.Empty ||
            command.OccurredAt.Kind != DateTimeKind.Utc)
        {
            return command.OccurredAt.Kind != DateTimeKind.Utc
                ? Errors.Book.InvalidSaleTimestamp()
                : Error.Validation("Book.InvalidClientGestureId", "A client gesture identifier is required.");
        }

        var receivedAt = dateTimeProvider.UtcNow;
        if (receivedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidSaleTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingMovement = await dbContext.BookMovements
            .SingleOrDefaultAsync(
                movement => movement.ClientGestureId == command.ClientGestureId,
                cancellationToken);

        if (existingMovement is not null)
        {
            if (existingMovement.Type != BookMovementType.Sale)
            {
                return Errors.Book.ClientGestureAlreadyUsed(command.ClientGestureId);
            }

            var existingResult = await BuildExistingResultAsync(existingMovement, cancellationToken);
            if (existingResult.IsError)
            {
                return existingResult.Errors;
            }

            await transaction.CommitAsync(cancellationToken);
            return existingResult.Value;
        }

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);

        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);

            if (book is null)
            {
                return Errors.Book.RedirectTargetNotFound(isbn13.Value);
            }
        }

        if (book is null)
        {
            book = Book.Create(isbn13, receivedAt);
            dbContext.Books.Add(book);
        }

        var (occurredAt, clockSuspect) = NormalizeClientTimestamp(command.OccurredAt, receivedAt);
        var fairs = await dbContext.AssoEvents.ToListAsync(cancellationToken);
        var fairMatch = BookFairResolver.Resolve(fairs, occurredAt);
        var hadUnreleasedAnnouncement = await dbContext.BookAnnouncements
            .AnyAsync(
                announcement =>
                    announcement.Isbn13 == isbn13 &&
                    announcement.Status == BookAnnouncementStatus.Announced,
                cancellationToken);
        var hadNoAvailableStock = book.QuantityAvailable < command.Quantity;

        book.RecordSale(occurredAt, command.Quantity);

        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            BookMovementType.Sale,
            -command.Quantity,
            occurredAt,
            receivedAt,
            clockSuspect,
            scanSessionId: null,
            command.VolunteerId,
            fairMatch.AssoEventsId,
            fairMatch.Note,
            command.ClientGestureId);
        dbContext.BookMovements.Add(movement);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RegisterSaleResult(
            isbn13.Value,
            movement.Id,
            command.Quantity,
            book.QuantityAvailable,
            book.SalesCount,
            fairMatch.AssoEventsId,
            fairMatch.Status,
            hadNoAvailableStock,
            hadUnreleasedAnnouncement,
            book.IsRare,
            clockSuspect,
            AlreadyProcessed: false);
    }

    private async Task<ErrorOr<RegisterSaleResult>> BuildExistingResultAsync(
        BookMovement movement,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == movement.Isbn13, cancellationToken);

        if (book is null)
        {
            return Error.Unexpected(
                "Book.MovementWithoutBook",
                "The idempotent sale points to a missing book.");
        }

        return new RegisterSaleResult(
            movement.Isbn13.Value,
            movement.Id,
            Math.Abs(movement.Quantity),
            book.QuantityAvailable,
            book.SalesCount,
            movement.AssoEventsId,
            BookFairResolver.FromNote(movement.Note),
            HadNoAvailableStock: false,
            HadUnreleasedAnnouncement: false,
            book.IsRare,
            movement.ClockSuspect,
            AlreadyProcessed: true);
    }

    private static (DateTime OccurredAt, bool ClockSuspect) NormalizeClientTimestamp(
        DateTime clientTimestamp,
        DateTime receivedAt)
    {
        var clockSuspect = clientTimestamp > receivedAt;
        return clockSuspect
            ? (receivedAt, true)
            : (clientTimestamp, false);
    }
}
