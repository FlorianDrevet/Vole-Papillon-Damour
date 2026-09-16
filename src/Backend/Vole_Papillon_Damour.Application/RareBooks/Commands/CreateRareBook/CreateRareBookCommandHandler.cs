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

        var clockError = RareBookCommandSupport.ValidateClock(dateTimeProvider, out var nowUtc);
        if (clockError is not null)
        {
            return clockError.Value;
        }

        var parseError = RareBookCommandSupport.ParseDetails(
            command.Shelf,
            command.Condition,
            command.Isbn13,
            out var shelf,
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
                    shelf,
                    command.Price,
                    condition,
                    command.PublicDescription,
                    command.Binding,
                    command.Dimensions,
                    command.PageCount,
                    command.ShelfLocation,
                    command.PriceSetBy,
                    nowUtc,
                    command.UserId,
                    isbn13,
                    collisionSuffix);
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
