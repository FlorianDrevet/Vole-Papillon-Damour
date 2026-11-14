using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.FairDigestAggregate;

namespace Vole_Papillon_Damour.Application.FairDigest.Queries.GetFairDigestStatus;

public sealed record GetFairDigestStatusQuery : IRequest<ErrorOr<FairDigestStatusResult>>;

public sealed record FairDigestStatusResult(
    bool Enabled,
    Guid? NextFairId,
    string? NextFairName,
    DateTimeOffset? NextFairStartsAt,
    Guid? CampaignId,
    DateTimeOffset? PlannedAtUtc,
    bool Suspended,
    int EstimatedRecipients,
    FairDigestCampaignReport? LastReport);

public sealed record FairDigestCampaignReport(
    string FairName,
    DateTimeOffset CompletedAtUtc,
    int SentCount,
    int SkippedCount,
    int FailedCount);
