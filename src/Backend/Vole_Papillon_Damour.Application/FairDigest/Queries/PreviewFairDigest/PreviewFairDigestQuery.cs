using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Queries.PreviewFairDigest;

public sealed record PreviewFairDigestQuery(UserId AdministratorId)
    : IRequest<ErrorOr<FairDigestPreviewResult>>;

public sealed record FairDigestPreviewResult(
    Guid CampaignId,
    FairDigestEvent Fair,
    FairDigestMemberContent Content,
    bool IsExample);
