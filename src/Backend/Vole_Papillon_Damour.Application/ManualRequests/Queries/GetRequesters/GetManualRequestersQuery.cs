using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequesters;

public sealed record GetManualRequestersQuery(
    string? Search,
    int Page,
    int PageSize) : IRequest<ErrorOr<ManualRequesterPageResult>>;
