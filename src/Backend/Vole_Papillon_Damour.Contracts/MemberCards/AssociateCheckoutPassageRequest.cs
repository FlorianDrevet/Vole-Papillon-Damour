namespace Vole_Papillon_Damour.Contracts.MemberCards;

public sealed record AssociateCheckoutPassageRequest(
    string Credential,
    DateTimeOffset OccurredAt);
