using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;

public sealed record RemoveWatchlistItemCommand(
    Guid ExternalId,
    string Email,
    Guid ItemId) : IRequest<ErrorOr<Success>>;
