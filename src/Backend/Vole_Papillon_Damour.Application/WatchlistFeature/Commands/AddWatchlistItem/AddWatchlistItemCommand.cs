using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;

public sealed record AddWatchlistItemCommand(
    Guid ExternalId,
    string Email,
    WatchlistItemScope Scope,
    string? WorkId,
    string? Isbn13) : IRequest<ErrorOr<AddedWatchlistItemResult>>;
