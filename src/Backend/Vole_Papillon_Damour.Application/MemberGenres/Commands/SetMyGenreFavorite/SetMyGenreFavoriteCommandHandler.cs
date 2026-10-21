using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberGenres.Common;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.MemberGenres.Commands.SetMyGenreFavorite;

public sealed class SetMyGenreFavoriteCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<SetMyGenreFavoriteCommand, ErrorOr<MemberGenreFavoritesResult>>
{
    public async Task<ErrorOr<MemberGenreFavoritesResult>> Handle(
        SetMyGenreFavoriteCommand command,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(command.ExternalId, out var externalId) ||
            externalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Email))
        {
            return Error.Validation("MemberGenreFavorites.InvalidMember", "A valid member identity is required.");
        }

        if (string.IsNullOrWhiteSpace(command.Genre) || command.Genre.Trim().Length > 100)
        {
            return Error.Validation("MemberGenreFavorites.InvalidGenre", "A genre is required and cannot exceed 100 characters.");
        }

        var genre = string.Join(
            ' ',
            command.Genre.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var member = await memberIdentityService.EnsureAsync(
            externalId,
            command.Email,
            command.FirstName,
            command.LastName,
            cancellationToken);
        var favorite = await dbContext.MemberGenreFavorites
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == member.Id && candidate.Genre == genre,
                cancellationToken);

        var changed = false;
        if (command.IsFavorite && favorite is null)
        {
            dbContext.MemberGenreFavorites.Add(MemberGenreFavorite.Create(member.Id, genre));
            changed = true;
        }
        else if (!command.IsFavorite && favorite is not null)
        {
            dbContext.MemberGenreFavorites.Remove(favorite);
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var genres = await dbContext.MemberGenreFavorites
            .AsNoTracking()
            .Where(candidate => candidate.UserId == member.Id)
            .OrderBy(candidate => candidate.Genre)
            .Select(candidate => candidate.Genre)
            .ToArrayAsync(cancellationToken);
        return new MemberGenreFavoritesResult(genres);
    }
}
