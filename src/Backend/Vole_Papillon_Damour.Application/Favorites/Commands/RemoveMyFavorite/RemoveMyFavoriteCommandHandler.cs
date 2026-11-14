using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.RemoveMyFavorite;

public sealed class RemoveMyFavoriteCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider)
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
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        var book = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == isbn13,
            cancellationToken);
        book?.RecordFavoriteCountChanged(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Deleted;
    }
}
