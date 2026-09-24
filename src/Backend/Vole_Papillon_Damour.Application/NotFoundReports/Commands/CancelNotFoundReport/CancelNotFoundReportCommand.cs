using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.CancelNotFoundReport;

public sealed record CancelNotFoundReportCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid ReportId) : IRequest<ErrorOr<Deleted>>;
