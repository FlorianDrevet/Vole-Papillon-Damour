using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Favorites.Common;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.WithdrawAnonymousFavorite;

public sealed record WithdrawAnonymousFavoriteCommand(string Isbn13)
    : IRequest<ErrorOr<AnonymousFavoriteResult>>;
