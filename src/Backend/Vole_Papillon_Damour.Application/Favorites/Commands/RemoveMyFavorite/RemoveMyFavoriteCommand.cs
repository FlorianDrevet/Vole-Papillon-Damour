using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.RemoveMyFavorite;

public sealed record RemoveMyFavoriteCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    string Isbn13) : IRequest<ErrorOr<Deleted>>;
