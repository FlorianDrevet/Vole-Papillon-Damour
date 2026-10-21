using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.MemberGenres.Common;

namespace Vole_Papillon_Damour.Application.MemberGenres.Commands.SetMyGenreFavorite;

public sealed record SetMyGenreFavoriteCommand(
    string ExternalId,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Genre,
    bool IsFavorite) : IRequest<ErrorOr<MemberGenreFavoritesResult>>;
