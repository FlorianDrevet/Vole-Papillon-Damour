using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RecordEmailBounce;

public sealed class RecordEmailBounceCommandHandler(IProjectDbContext dbContext)
    : IRequestHandler<RecordEmailBounceCommand, ErrorOr<RecordEmailBounceResult>>
{
    public async Task<ErrorOr<RecordEmailBounceResult>> Handle(
        RecordEmailBounceCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MemberId is null || command.MemberId.Value == Guid.Empty)
        {
            return Error.Validation(
                "Watchlist.InvalidMemberId",
                "A valid member identifier is required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var watchlist = await dbContext.Watchlists
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.MemberId,
                cancellationToken);
        if (watchlist is null)
        {
            return Errors.Watchlist.NotFound(command.MemberId.Value);
        }

        watchlist.RecordEmailBounce();

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RecordEmailBounceResult.From(watchlist);
    }
}
