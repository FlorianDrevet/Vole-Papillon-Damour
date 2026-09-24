using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetClosedNotFoundReports;

public sealed record GetClosedNotFoundReportsQuery(int Page, int PageSize, bool RareOnly)
    : IRequest<ErrorOr<ClosedNotFoundReportsResult>>;
