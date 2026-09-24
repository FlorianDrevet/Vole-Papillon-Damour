using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UpdateRareBook;

public sealed class UpdateRareBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateRareBookCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        UpdateRareBookCommand command,
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
            command.Condition,
            command.Isbn13,
            out var condition,
            out var isbn13);
        if (parseError is not null)
        {
            return parseError.Value;
        }

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        if (!RareBookCommandSupport.MatchesRowVersion(rareBook.RowVersion, command.RowVersion))
        {
            return Errors.RareBook.ConcurrencyConflict(command.RareBookId.Value);
        }

        if (isbn13 is not null && await dbContext.RareBooks
                .AsNoTracking()
                .AnyAsync(book => book.Id != command.RareBookId && book.Isbn13 == isbn13, cancellationToken))
        {
            return Errors.RareBook.DuplicateIsbn(isbn13.Value.Value);
        }

        bool changed;
        try
        {
            changed = rareBook.Update(
                command.Title,
                command.AuthorMention,
                command.Publisher,
                command.PublicationYear,
                command.Price,
                condition,
                command.PublicDescription,
                nowUtc,
                command.UserId,
                isbn13);
        }
        catch (ArgumentException exception)
        {
            return Errors.RareBook.InvalidData(exception.Message);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return RareBookProjector.ToResult(rareBook);
    }
}
