using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;

public sealed class RemoveWatchlistItemCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<RemoveWatchlistItemCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        RemoveWatchlistItemCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var item = await dbContext.WatchlistItems.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == command.ItemId &&
                candidate.UserId == user.Id,
            cancellationToken);
        if (item is null)
        {
            return Errors.Watchlist.ItemNotFound(command.ItemId);
        }

        dbContext.WatchlistItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success;
    }
}
