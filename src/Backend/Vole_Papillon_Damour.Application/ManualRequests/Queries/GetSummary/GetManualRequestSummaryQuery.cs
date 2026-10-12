using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.ManualRequests.Queries.GetSummary;

public sealed record GetManualRequestSummaryQuery : IRequest<ErrorOr<ManualRequestSummaryResult>>;
