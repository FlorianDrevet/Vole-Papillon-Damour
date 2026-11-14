using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Favorites.Common;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.DeclareAnonymousFavorite;

public sealed record DeclareAnonymousFavoriteCommand(string Isbn13)
    : IRequest<ErrorOr<AnonymousFavoriteResult>>;
