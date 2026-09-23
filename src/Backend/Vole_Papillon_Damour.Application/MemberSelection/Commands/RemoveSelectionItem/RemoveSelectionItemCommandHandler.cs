using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;

public sealed class RemoveSelectionItemCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<RemoveSelectionItemCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(
        RemoveSelectionItemCommand command,
        CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var item = await dbContext.MemberSelectionItems.SingleOrDefaultAsync(
            candidate => candidate.Id == command.ItemId && candidate.UserId == user.Id,
            cancellationToken);
        if (item is null)
        {
            return Errors.MemberSelection.NotFound(command.ItemId);
        }

        dbContext.MemberSelectionItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }
}
