using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Favorites.Common;

namespace Vole_Papillon_Damour.Application.Favorites.Queries.GetMyFavorites;

public sealed record GetMyFavoritesQuery(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName) : IRequest<ErrorOr<MyFavoritesResult>>;
