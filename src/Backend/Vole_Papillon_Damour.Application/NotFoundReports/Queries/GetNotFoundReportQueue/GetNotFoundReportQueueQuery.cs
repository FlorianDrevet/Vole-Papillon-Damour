using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportQueue;

public sealed record GetNotFoundReportQueueQuery(
    string Kind,
    string Sort,
    int Page,
    int PageSize,
    bool RareOnly) : IRequest<ErrorOr<NotFoundReportQueueResult>>;
