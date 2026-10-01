using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.ReportNotFound;

public sealed record ReportNotFoundCommand(
    Guid ExternalId,
    string Email,
    string? FirstName,
    string? LastName,
    Guid SelectionItemId,
    string? Comment) : IRequest<ErrorOr<NotFoundReportCreatedResult>>;
