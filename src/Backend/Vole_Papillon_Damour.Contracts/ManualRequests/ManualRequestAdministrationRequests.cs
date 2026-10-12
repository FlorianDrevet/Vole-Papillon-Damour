namespace Vole_Papillon_Damour.Contracts.ManualRequests;

public sealed record CreateManualRequesterRequest(
    string LastName,
    string FirstName,
    string Phone,
    bool ConsentGiven);

public sealed record UpdateManualRequesterRequest(
    string LastName,
    string FirstName,
    string Phone);

public sealed record AddManualBookRequestRequest(
    string Scope,
    string? WorkId,
    string? Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    bool PickFromStock);

public sealed record RefuseManualRequestRequest(bool KeepSearching);
