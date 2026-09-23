namespace Vole_Papillon_Damour.Contracts.MemberCards;

public sealed record MemberCardResponse(
    string QrPayload,
    string RecoveryCode,
    string DisplayLabel,
    DateTimeOffset IssuedAt);
