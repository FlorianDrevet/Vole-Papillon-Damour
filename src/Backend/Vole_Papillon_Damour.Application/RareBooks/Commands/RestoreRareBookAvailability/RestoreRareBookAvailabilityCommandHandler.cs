using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.RareBooks.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.RareBooks.Commands.RestoreRareBookAvailability;

public sealed class RestoreRareBookAvailabilityCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<RestoreRareBookAvailabilityCommand, ErrorOr<RareBookResult>>
{
    public async Task<ErrorOr<RareBookResult>> Handle(
        RestoreRareBookAvailabilityCommand command,
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

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
            changed = rareBook.RestoreAvailability(nowUtc, command.UserId);
        }
        catch (InvalidOperationException)
        {
            return Errors.RareBook.RestoreWindowExpired(command.RareBookId.Value);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await bookAlertOutbox.CancelPendingForRareBookAsync(
                rareBook.Id,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return RareBookProjector.ToResult(rareBook);
    }
}
