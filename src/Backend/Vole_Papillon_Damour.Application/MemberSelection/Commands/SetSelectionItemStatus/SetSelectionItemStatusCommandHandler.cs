using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.SetSelectionItemStatus;

public sealed class SetSelectionItemStatusCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SetSelectionItemStatusCommand, ErrorOr<Updated>>
{
    public async Task<ErrorOr<Updated>> Handle(
        SetSelectionItemStatusCommand command,
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

        if (!Enum.IsDefined(command.Status))
        {
            return Errors.MemberSelection.InvalidStatus();
        }

        item.ChangeStatus(command.Status, dateTimeProvider.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Updated;
    }
}
