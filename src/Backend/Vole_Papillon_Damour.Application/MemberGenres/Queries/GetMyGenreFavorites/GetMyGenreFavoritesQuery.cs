using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberGenres.Common;

namespace Vole_Papillon_Damour.Application.MemberGenres.Queries.GetMyGenreFavorites;

public sealed record GetMyGenreFavoritesQuery(
    string ExternalId,
    string? Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<MemberGenreFavoritesResult>>;
