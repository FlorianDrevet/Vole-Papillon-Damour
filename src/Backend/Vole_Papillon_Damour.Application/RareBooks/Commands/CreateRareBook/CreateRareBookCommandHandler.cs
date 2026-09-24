using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RareBookAggregate;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.CreateRareBook;

public sealed class CreateRareBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateRareBookCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        CreateRareBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!RareBookCommandSupport.IsValidUser(command.UserId))
        {
            return Errors.RareBook.InvalidUser();
        }

        if (command.ClientGestureId == Guid.Empty)
        {
            return Errors.RareBook.InvalidData("A client gesture identifier must be non-empty when provided.");
        }

        var clockError = RareBookCommandSupport.ValidateClock(dateTimeProvider, out var nowUtc);
        if (clockError is not null)
        {
            return clockError.Value;
        }

        if (command.ClientGestureId is { } clientGestureId)
        {
            var existing = await dbContext.RareBooks
                .Include(book => book.Photos)
                .SingleOrDefaultAsync(
                    book => book.ClientGestureId == clientGestureId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.CreatedBy == command.UserId
                    ? RareBookProjector.ToResult(existing)
                    : Errors.RareBook.ClientGestureAlreadyUsed(clientGestureId);
            }
        }

        var parseError = RareBookCommandSupport.ParseDetails(
            command.Condition,
            command.Isbn13,
            out var condition,
            out var isbn13);
        if (parseError is not null)
        {
            return parseError.Value;
        }

        if (isbn13 is not null && await dbContext.RareBooks
                .AsNoTracking()
                .AnyAsync(book => book.Isbn13 == isbn13, cancellationToken))
        {
            return Errors.RareBook.DuplicateIsbn(isbn13.Value.Value);
        }

        RareBook? rareBook = null;
        for (var collisionSuffix = 1; collisionSuffix <= 10_000; collisionSuffix++)
        {
            try
            {
                rareBook = RareBook.CreateWithDetails(
                    command.Title,
                    command.AuthorMention,
                    command.Publisher,
                    command.PublicationYear,
                    command.Price,
                    condition,
                    command.PublicDescription,
                    nowUtc,
                    command.UserId,
                    isbn13,
                    collisionSuffix,
                    command.ClientGestureId);
            }
            catch (ArgumentException exception)
            {
                return Errors.RareBook.InvalidData(exception.Message);
            }

            var slugAlreadyUsed = await dbContext.RareBooks
                .AsNoTracking()
                .AnyAsync(book => book.Slug == rareBook.Slug, cancellationToken);
            if (!slugAlreadyUsed)
            {
                break;
            }

            rareBook = null;
        }

        if (rareBook is null)
        {
            return Errors.RareBook.SlugConflict(command.Title);
        }

        dbContext.RareBooks.Add(rareBook);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RareBookProjector.ToResult(rareBook);
    }
}
