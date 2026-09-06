using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminFairStatsQuery(Guid FairId) : IRequest<ErrorOr<AdminFairStatsResult>>;
