using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Queries.GetMyWatchlist;

public sealed record GetMyWatchlistQuery(
    Guid ExternalId,
    string Email,
    string? FirstName = null,
    string? LastName = null) : IRequest<ErrorOr<MyWatchlistResult>>;
