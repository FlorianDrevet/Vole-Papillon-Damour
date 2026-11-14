using ErrorOr;
using MediatR;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SendDueFairDigests;

public sealed record SendDueFairDigestsCommand : IRequest<ErrorOr<FairDigestSendResult>>;

public sealed record FairDigestSendResult(
    bool Disabled,
    int CampaignCount,
    int SentCount,
    int SkippedCount,
    int FailedCount);
