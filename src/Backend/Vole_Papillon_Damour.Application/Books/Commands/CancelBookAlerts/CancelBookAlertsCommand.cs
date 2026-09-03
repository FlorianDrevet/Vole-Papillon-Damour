using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.CancelBookAlerts;

public sealed record CancelBookAlertsCommand(
    ScanSessionId ScanSessionId,
    UserId UpdatedBy) : IRequest<ErrorOr<BookAlertOperationResult>>;
