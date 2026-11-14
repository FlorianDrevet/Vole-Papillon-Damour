using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Favorites.Common;

namespace Vole_Papillon_Damour.Application.Favorites.Commands.MergeMyFavorites;

public sealed record MergeMyFavoritesCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyList<MergeMyFavoritesEntry> Entries) : IRequest<ErrorOr<MergeMyFavoritesResult>>;
