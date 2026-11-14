using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.FairDigest.Commands.SendFairDigestTest;

public sealed record SendFairDigestTestCommand(UserId AdministratorId)
    : IRequest<ErrorOr<FairDigestTestSendResult>>;

public sealed record FairDigestTestSendResult(string Email, bool IsExample);
