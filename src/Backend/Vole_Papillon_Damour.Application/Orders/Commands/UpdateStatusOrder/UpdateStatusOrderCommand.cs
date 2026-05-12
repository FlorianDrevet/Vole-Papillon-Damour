using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Orders.Common;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Orders.Commands.UpdateStatusOrder;

public record UpdateStatusOrderCommand(
    OrderId OrderId,
    StatusEnum Status
) : IRequest<ErrorOr<OrderResult>>;