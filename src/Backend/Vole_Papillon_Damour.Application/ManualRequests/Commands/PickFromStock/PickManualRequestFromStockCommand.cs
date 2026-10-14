using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.PickFromStock;

public sealed record PickManualRequestFromStockCommand(Guid RequestId, UserId By)
    : IRequest<ErrorOr<Success>>;
