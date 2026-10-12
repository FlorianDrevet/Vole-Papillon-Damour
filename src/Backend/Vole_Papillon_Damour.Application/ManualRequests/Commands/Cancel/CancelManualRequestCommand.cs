using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.Cancel;

public sealed record CancelManualRequestCommand(Guid RequestId, UserId By)
    : IRequest<ErrorOr<ManualRequestReleaseResult>>;
