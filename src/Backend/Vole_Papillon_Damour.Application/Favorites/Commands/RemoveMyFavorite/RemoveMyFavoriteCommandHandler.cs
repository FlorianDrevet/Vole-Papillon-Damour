using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.RemoveMyFavorite;

public sealed class RemoveMyFavoriteCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<RemoveMyFavoriteCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(
        RemoveMyFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13))
        {
            return Errors.Favorites.InvalidIsbn(command.Isbn13);
        }

        var user = await memberIdentityService.EnsureAsync(
            command.ExternalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var favorite = await dbContext.MemberFavoriteBooks.SingleOrDefaultAsync(
            candidate => candidate.UserId == user.Id && candidate.Isbn13 == isbn13,
            cancellationToken);
        if (favorite is null)
        {
            return Result.Deleted;
        }

        dbContext.MemberFavoriteBooks.Remove(favorite);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }
}
