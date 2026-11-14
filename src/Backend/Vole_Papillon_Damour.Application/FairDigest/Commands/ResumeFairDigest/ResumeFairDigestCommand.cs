using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SuspendFairDigest;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.ResumeFairDigest;

public sealed record ResumeFairDigestCommand(UserId AdministratorId)
    : IRequest<ErrorOr<FairDigestCampaignOperationResult>>;
