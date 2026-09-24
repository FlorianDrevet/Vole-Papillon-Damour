namespace Vole_Papillon_Damour.Contracts.CheckoutPassages;

public sealed record CheckoutPassageLookupResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    int LineCount,
    string? DisplayLabel);
