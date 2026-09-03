using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanBookResult(
    string Isbn13,
    BookVerdictDecision Verdict,
    int QuantityAvailable,
    int QuantityAnnounced,
    ScanSessionId ScanSessionId,
    BookMovementType MovementType,
    bool AlreadyProcessed,
    bool ClockSuspect);
