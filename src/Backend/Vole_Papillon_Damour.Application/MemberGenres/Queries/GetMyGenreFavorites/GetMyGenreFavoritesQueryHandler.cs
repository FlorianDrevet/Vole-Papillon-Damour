using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberGenres.Common;

namespace Vole_Papillon_Damour.Application.MemberGenres.Queries.GetMyGenreFavorites;

public sealed class GetMyGenreFavoritesQueryHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService)
    : IRequestHandler<GetMyGenreFavoritesQuery, ErrorOr<MemberGenreFavoritesResult>>
{
    public async Task<ErrorOr<MemberGenreFavoritesResult>> Handle(
        GetMyGenreFavoritesQuery query,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(query.ExternalId, out var externalId) ||
            externalId == Guid.Empty ||
            string.IsNullOrWhiteSpace(query.Email))
        {
            return Error.Validation("MemberGenreFavorites.InvalidMember", "A valid member identity is required.");
        }

        var member = await memberIdentityService.EnsureAsync(
            externalId,
            query.Email,
            query.FirstName,
            query.LastName,
            cancellationToken);
        var genres = await dbContext.MemberGenreFavorites
            .AsNoTracking()
            .Where(favorite => favorite.UserId == member.Id)
            .OrderBy(favorite => favorite.Genre)
            .Select(favorite => favorite.Genre)
            .ToArrayAsync(cancellationToken);

        return new MemberGenreFavoritesResult(genres);
    }
}
