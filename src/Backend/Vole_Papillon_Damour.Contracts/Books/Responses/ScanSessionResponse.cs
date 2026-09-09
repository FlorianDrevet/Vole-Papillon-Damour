namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanSessionResponse(
    Guid ScanSessionId,
    Guid VolunteerId,
    string Mode,
    Guid? TargetAssoEventsId,
    DateTime StartedAt,
    DateTime LastScanAt,
    DateTime LastSyncAt,
    bool LateArrivals,
    DateTime? EndedAt,
    string? CloseReason,
    string Status,
    bool ReusedExistingSession,
    int ScannedCount,
    int KeptCount,
    int RejectedCount);
