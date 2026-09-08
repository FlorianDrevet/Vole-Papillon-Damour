using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed record CloseScanSessionCommand(
    ScanSessionId ScanSessionId,
    ScanCloseReason CloseReason,
    UserId VolunteerId) : IRequest<ErrorOr<ScanSessionResult>>;
