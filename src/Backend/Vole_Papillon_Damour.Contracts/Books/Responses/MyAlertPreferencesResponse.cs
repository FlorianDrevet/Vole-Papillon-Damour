namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record MyAlertPreferencesResponse(
    string AlertStatus,
    int BounceCount,
    bool Changed);
