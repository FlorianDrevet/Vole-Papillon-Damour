using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminBookFairsQuery(
    bool IncludeCancelled = false,
    int Page = 1,
    int PageSize = 50) : IRequest<ErrorOr<AdminFairPageResult>>;
