using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.PermanentlyDeleteBook;

public sealed class PermanentlyDeleteBookCommandHandler(IProjectDbContext dbContext)
    : IRequestHandler<PermanentlyDeleteBookCommand, ErrorOr<DeleteBookResult>>
{
    public async Task<ErrorOr<DeleteBookResult>> Handle(
        PermanentlyDeleteBookCommand command,
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

        var hasSaleHistory = await dbContext.BookMovements
                .AnyAsync(
                    movement => movement.Isbn13 == isbn13 && movement.Type == BookMovementType.Sale,
                    cancellationToken) ||
            await dbContext.CheckoutPassageLines
                .AnyAsync(line => line.Isbn13 == isbn13, cancellationToken);
        if (hasSaleHistory)
        {
            return Errors.Book.BookHasSales(isbn13.Value);
        }

        var hasRedirect = book.RedirectedToIsbn13 is not null ||
                          await dbContext.Books.AnyAsync(
                              candidate => candidate.RedirectedToIsbn13 == isbn13,
                              cancellationToken);
        if (hasRedirect)
        {
            return Errors.Book.BookHasHistory(isbn13.Value);
        }

        var hasAnnouncements = await dbContext.BookAnnouncements
            .AnyAsync(announcement => announcement.Isbn13 == isbn13, cancellationToken);
        var hasSessionOrFairHistory = await dbContext.BookMovements
            .AnyAsync(
                movement => movement.Isbn13 == isbn13 &&
                            (movement.ScanSessionId != null || movement.AssoEventsId != null),
                cancellationToken);
        var hasFoundManualRequest = await dbContext.ManualBookRequests
            .AnyAsync(request => request.FoundIsbn13 == isbn13, cancellationToken);
        if (hasAnnouncements || hasSessionOrFairHistory || hasFoundManualRequest)
        {
            return Errors.Book.BookHasHistory(isbn13.Value);
        }

        var movements = await dbContext.BookMovements
            .Where(movement => movement.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var reports = await dbContext.BookNotFoundReports
            .Where(report => report.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var selections = await dbContext.MemberSelectionItems
            .Where(item => item.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var watchlistItems = await dbContext.WatchlistItems
            .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var alertHistory = await dbContext.UserAlertHistories
            .Where(history => history.Isbn13 == isbn13)
            .ToListAsync(cancellationToken);
        var similarityProfiles = await dbContext.BookSimilarityProfiles
            .Where(profile => profile.Isbn13 == isbn13.Value)
            .ToListAsync(cancellationToken);
        var neighborLinks = await dbContext.BookNeighbors
            .Where(neighbor => neighbor.Isbn13 == isbn13.Value || neighbor.NeighborIsbn13 == isbn13.Value)
            .ToListAsync(cancellationToken);

        dbContext.BookMovements.RemoveRange(movements);
        dbContext.BookNotFoundReports.RemoveRange(reports);
        dbContext.MemberSelectionItems.RemoveRange(selections);
        dbContext.WatchlistItems.RemoveRange(watchlistItems);
        dbContext.UserAlertHistories.RemoveRange(alertHistory);
        dbContext.BookSimilarityProfiles.RemoveRange(similarityProfiles);
        dbContext.BookNeighbors.RemoveRange(neighborLinks);
        dbContext.Books.Remove(book);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new DeleteBookResult(isbn13.Value, Deleted: true);
    }
}
