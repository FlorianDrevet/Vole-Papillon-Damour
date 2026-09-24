namespace Vole_Papillon_Damour.Contracts.MemberCards;

public sealed record AssociateCheckoutPassageResponse(
    Guid CheckoutPassageId,
    string Status,
    string? DisplayLabel,
    bool AlreadyProcessed);
