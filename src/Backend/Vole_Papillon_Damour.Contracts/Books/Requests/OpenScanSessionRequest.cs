namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record OpenScanSessionRequest(
    string Mode,
    Guid? TargetAssoEventsId,
    Guid? ClientSessionId);
