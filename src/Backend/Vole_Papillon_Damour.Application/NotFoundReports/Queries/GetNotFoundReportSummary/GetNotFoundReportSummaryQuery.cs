using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Queries.GetNotFoundReportSummary;

public sealed record GetNotFoundReportSummaryQuery(
    string? Isbn13,
    Guid? RareBookId,
    bool RareOnly) : IRequest<ErrorOr<NotFoundReportSummaryResult>>;
