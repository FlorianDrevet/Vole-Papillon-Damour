using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.VoidSale;

public sealed class VoidSaleCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<VoidSaleCommand, ErrorOr<VoidSaleResult>>
{
    public async Task<ErrorOr<VoidSaleResult>> Handle(
        VoidSaleCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SaleMovementId is null)
        {
            return Errors.Book.SaleNotFound(Guid.Empty);
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
            if (existingMovement.Type != BookMovementType.Correction ||
                existingMovement.ReversalOfMovementId is null)
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

        var sale = await dbContext.BookMovements
            .SingleOrDefaultAsync(
                movement => movement.Id == command.SaleMovementId,
                cancellationToken);
        if (sale is null)
        {
            return Errors.Book.SaleNotFound(command.SaleMovementId.Value);
        }

        if (sale.Type != BookMovementType.Sale || sale.Quantity >= 0)
        {
            return Errors.Book.NotASaleMovement(command.SaleMovementId.Value);
        }

        var alreadyVoided = await dbContext.BookMovements
            .AnyAsync(
                movement => movement.ReversalOfMovementId == sale.Id,
                cancellationToken);
        if (alreadyVoided)
        {
            return Errors.Book.SaleAlreadyVoided(command.SaleMovementId.Value);
        }

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == sale.Isbn13, cancellationToken);
        if (book is null)
        {
            return Error.Unexpected(
                "Book.MovementWithoutBook",
                "The sale points to a missing book.");
        }

        var fairs = await dbContext.AssoEvents.ToListAsync(cancellationToken);
        if (!IsCancellationAllowed(fairs, sale.AssoEventsId, receivedAt))
        {
            return Errors.Book.SaleCancellationOutsideOpenFair();
        }

        var (occurredAt, clockSuspect) = NormalizeClientTimestamp(command.OccurredAt, receivedAt);
        var quantity = Math.Abs(sale.Quantity);
        book.ReverseSale(occurredAt, quantity);

        var reversal = BookMovement.Create(
            BookMovementId.CreateUnique(),
            sale.Isbn13,
            BookMovementType.Correction,
            quantity,
            occurredAt,
            receivedAt,
            clockSuspect,
            scanSessionId: null,
            command.VolunteerId,
            sale.AssoEventsId,
            "Sale.Void",
            command.ClientGestureId,
            sale.Id);
        dbContext.BookMovements.Add(reversal);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new VoidSaleResult(
            sale.Isbn13.Value,
            sale.Id,
            reversal.Id,
            quantity,
            book.QuantityAvailable,
            book.SalesCount,
            sale.AssoEventsId,
            clockSuspect,
            AlreadyProcessed: false);
    }

    private async Task<ErrorOr<VoidSaleResult>> BuildExistingResultAsync(
        BookMovement reversal,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == reversal.Isbn13, cancellationToken);
        if (book is null || reversal.ReversalOfMovementId is null)
        {
            return Error.Unexpected(
                "Book.ReversalWithoutBook",
                "The idempotent sale reversal points to missing data.");
        }

        return new VoidSaleResult(
            reversal.Isbn13.Value,
            reversal.ReversalOfMovementId,
            reversal.Id,
            reversal.Quantity,
            book.QuantityAvailable,
            book.SalesCount,
            reversal.AssoEventsId,
            reversal.ClockSuspect,
            AlreadyProcessed: true);
    }

    private static bool IsCancellationAllowed(
        IEnumerable<Vole_Papillon_Damour.Domain.AssoEventsAggregate.AssoEvents> fairs,
        Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.AssoEventsId? originalFairId,
        DateTime receivedAt)
    {
        if (originalFairId is not null)
        {
            var originalFair = fairs.SingleOrDefault(assoEvent => assoEvent.Id == originalFairId);
            return originalFair is not null && BookFairResolver.IsOpen(originalFair, receivedAt);
        }

        return BookFairResolver.Resolve(fairs, receivedAt).Status == SaleFairMatchStatus.Attached;
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
