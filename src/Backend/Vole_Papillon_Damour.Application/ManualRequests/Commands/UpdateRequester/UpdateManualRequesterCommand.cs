using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.UpdateRequester;

public sealed record UpdateManualRequesterCommand(
    Guid RequesterId,
    string LastName,
    string FirstName,
    string Phone,
    UserId By) : IRequest<ErrorOr<Success>>;
