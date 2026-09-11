using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;

public sealed record RemoveWatchlistItemCommand(
    Guid ExternalId,
    string Email,
    Guid ItemId,
    string? FirstName = null,
    string? LastName = null) : IRequest<ErrorOr<Success>>;
