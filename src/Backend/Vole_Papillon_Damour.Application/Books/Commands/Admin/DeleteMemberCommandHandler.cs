using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class DeleteMemberCommandHandler(
    IProjectDbContext dbContext,
    IAccountDeletionService accountDeletionService)
    : IRequestHandler<DeleteMemberCommand, ErrorOr<AdminMemberOperationResult>>
{
    public async Task<ErrorOr<AdminMemberOperationResult>> Handle(
        DeleteMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MemberId is null || command.MemberId.Value == Guid.Empty)
        {
            return Errors.Book.MemberNotFound(Guid.Empty);
        }

        if (command.DeletedBy is null || command.DeletedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidDeletedBy", "A deleting user identifier is required.");
        }

        var member = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(
            user => user.Id == command.MemberId,
            cancellationToken);
        if (member is null)
        {
            return Errors.Book.MemberNotFound(command.MemberId.Value);
        }

        if (string.IsNullOrWhiteSpace(member.ExternalId))
        {
            return new AdminMemberOperationResult(
                command.MemberId.Value,
                "Deleted",
                Changed: false,
                DeletionCompleted: true);
        }

        var result = await accountDeletionService.RequestAsync(
            member.ExternalId,
            cancellationToken);
        return new AdminMemberOperationResult(
            command.MemberId.Value,
            "DeletionRequested",
            Changed: true,
            result.IsCompleted);
    }
}
