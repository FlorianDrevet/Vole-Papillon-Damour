using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.DismissNotFound;

public sealed record DismissNotFoundTargetCommand(
    NotFoundReportTargetRef Target,
    string Note,
    UserId By) : IRequest<ErrorOr<NotFoundClosureResult>>;
