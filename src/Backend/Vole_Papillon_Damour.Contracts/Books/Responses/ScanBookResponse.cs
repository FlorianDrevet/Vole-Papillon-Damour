namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanBookResponse(
    string Isbn13,
    string Verdict,
    int QtyAvailable,
    int QtyAnnounced,
    Guid ScanSessionId,
    string MovementType,
    bool AlreadyProcessed,
    bool ClockSuspect);
