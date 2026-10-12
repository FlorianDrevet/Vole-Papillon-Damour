using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;

public sealed record CreateManualRequesterCommand(
    string LastName,
    string FirstName,
    string Phone,
    bool ConsentGiven,
    UserId By) : IRequest<ErrorOr<Guid>>;
