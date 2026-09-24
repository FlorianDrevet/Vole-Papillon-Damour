using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;

public sealed record ResolvedMemberCard(UserId UserId, string DisplayLabel);
