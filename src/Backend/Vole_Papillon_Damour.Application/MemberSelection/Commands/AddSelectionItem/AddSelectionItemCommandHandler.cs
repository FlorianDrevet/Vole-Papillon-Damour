using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;

namespace Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;

public sealed class AddSelectionItemCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AddSelectionItemCommand, ErrorOr<SelectionItemAddedResult>>
{
    public async Task<ErrorOr<SelectionItemAddedResult>> Handle(
        AddSelectionItemCommand command,
        CancellationToken cancellationToken)
    {
        var target = await SelectionTargetResolver.ResolveAsync(
            dbContext,
            command.Isbn13,
            command.RareBookId,
            cancellationToken);
        if (target.IsError)
        {
            return target.Errors;
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);

        var existingItemId = await dbContext.MemberSelectionItems
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .Where(item =>
                (target.Value.Isbn13 != null && item.Isbn13 == target.Value.Isbn13) ||
                (target.Value.RareBookId != null && item.RareBookId == target.Value.RareBookId))
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingItemId is { } existingId)
        {
            return new SelectionItemAddedResult(existingId, AlreadyPresent: true);
        }

        var itemCount = await dbContext.MemberSelectionItems
            .CountAsync(item => item.UserId == user.Id, cancellationToken);
        if (itemCount >= MemberSelectionItem.MaxItemsPerMember)
        {
            return Errors.MemberSelection.TooManyItems(MemberSelectionItem.MaxItemsPerMember);
        }

        var item = target.Value.Isbn13 is { } isbn13
            ? MemberSelectionItem.CreateForEdition(Guid.NewGuid(), user.Id, isbn13, dateTimeProvider.UtcNow)
            : MemberSelectionItem.CreateForRareBook(
                Guid.NewGuid(), user.Id, target.Value.RareBookId!, dateTimeProvider.UtcNow);
        dbContext.MemberSelectionItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SelectionItemAddedResult(item.Id, AlreadyPresent: false);
    }
}
