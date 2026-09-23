namespace Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;

public sealed record MyCardResult(
    string QrPayload,
    string RecoveryCode,
    string DisplayLabel,
    DateTimeOffset IssuedAt);
