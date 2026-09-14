using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminCatalogueFlowStatsQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IRequest<ErrorOr<AdminCatalogueFlowStatsResult>>;
