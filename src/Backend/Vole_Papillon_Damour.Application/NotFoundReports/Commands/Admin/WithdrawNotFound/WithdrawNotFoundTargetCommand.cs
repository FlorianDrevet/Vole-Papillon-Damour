using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.NotFoundReports.Commands.Admin.WithdrawNotFound;

public sealed record WithdrawNotFoundTargetCommand(
    NotFoundReportTargetRef Target,
    int QuantityFound,
    NotFoundWithdrawalReason Reason,
    string Note,
    UserId By) : IRequest<ErrorOr<NotFoundClosureResult>>;
