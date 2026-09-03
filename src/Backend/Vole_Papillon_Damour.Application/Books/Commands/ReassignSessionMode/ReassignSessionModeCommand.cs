using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;

public sealed record ReassignSessionModeCommand(
    ScanSessionId ScanSessionId,
    ScanMode TargetMode,
    AssoEventsId? TargetAssoEventsId,
    UserId UpdatedBy) : IRequest<ErrorOr<ReassignSessionModeResult>>;
