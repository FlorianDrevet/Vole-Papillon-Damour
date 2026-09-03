using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ForceBookAlerts;

public sealed record ForceBookAlertsCommand(
    ScanSessionId ScanSessionId,
    UserId UpdatedBy) : IRequest<ErrorOr<BookAlertOperationResult>>;
