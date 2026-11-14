using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record FairDigestEmailMessage(
    Guid CampaignId,
    UserId MemberId,
    string Email,
    string? FirstName,
    FairDigestEvent Fair,
    FairDigestMemberContent Content);
