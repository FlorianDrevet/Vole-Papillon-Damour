using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SuspendFairDigest;

public sealed record SuspendFairDigestCommand(UserId AdministratorId)
    : IRequest<ErrorOr<FairDigestCampaignOperationResult>>;

public sealed record FairDigestCampaignOperationResult(Guid CampaignId, bool Changed, DateTimeOffset? SuspendedAtUtc);
