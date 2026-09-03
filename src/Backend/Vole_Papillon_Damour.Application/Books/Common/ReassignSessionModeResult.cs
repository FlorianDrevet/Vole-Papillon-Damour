using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ReassignSessionModeResult(
    ScanSessionId ScanSessionId,
    ScanMode Mode,
    AssoEventsId? TargetAssoEventsId,
    int ReversedMovementCount,
    int ReplayedMovementCount);
