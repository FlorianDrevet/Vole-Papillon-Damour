using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Refuse;

public sealed record RefuseManualRequestCommand(Guid RequestId, bool KeepSearching, UserId By)
    : IRequest<ErrorOr<ManualRequestReleaseResult>>;
