using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.DeleteRequester;

public sealed record DeleteManualRequesterCommand(Guid RequesterId, UserId By)
    : IRequest<ErrorOr<ManualRequesterDeletionResult>>;
