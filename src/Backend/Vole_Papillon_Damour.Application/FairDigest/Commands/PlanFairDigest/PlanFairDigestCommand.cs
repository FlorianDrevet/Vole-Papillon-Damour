using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.FairDigest.Common;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.PlanFairDigest;

public sealed record PlanFairDigestCommand : IRequest<ErrorOr<FairDigestPlanResult>>;

public sealed record FairDigestPlanResult(
    FairDigestPlanStatus Status,
    Guid? CampaignId,
    Guid? AssoEventsId,
    DateTimeOffset? PlannedAtUtc);

public enum FairDigestPlanStatus
{
    Disabled,
    NoUpcomingFair,
    TooLate,
    Planned,
    AlreadyPlanned,
}
