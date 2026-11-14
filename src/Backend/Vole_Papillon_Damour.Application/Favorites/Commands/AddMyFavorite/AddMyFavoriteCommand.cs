using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Favorites.Common;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.AddMyFavorite;

public sealed record AddMyFavoriteCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    string Isbn13) : IRequest<ErrorOr<FavoriteAddedResult>>;
