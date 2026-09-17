using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.UnpublishRareBook;

public sealed class UnpublishRareBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UnpublishRareBookCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        UnpublishRareBookCommand command,
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

        var rareBook = await dbContext.RareBooks
            .Include(book => book.Photos)
            .SingleOrDefaultAsync(book => book.Id == command.RareBookId, cancellationToken);
        if (rareBook is null)
        {
            return Errors.RareBook.NotFound(command.RareBookId.Value);
        }

        bool changed;
        try
        {
            changed = rareBook.Unpublish(nowUtc, command.UserId);
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
