namespace Vole_Papillon_Damour.Contracts.ManualRequests;

public sealed record ManualRequestScanCandidateResponse(
    Guid RequestId,
    string FirstName,
    string LastName,
    string Phone);
