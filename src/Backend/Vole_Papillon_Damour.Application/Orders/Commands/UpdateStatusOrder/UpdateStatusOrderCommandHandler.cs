using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence.BaseRepository;
using Vole_Papillon_Damour.Application.Orders.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Orders.Commands.UpdateStatusOrder;

public class UpdateStatusOrderCommandHandler(IOrderRepository orderRepository, IMapper mapper)
    : IRequestHandler<UpdateStatusOrderCommand, ErrorOr<OrderResult>>
{
    public async Task<ErrorOr<OrderResult>> Handle(UpdateStatusOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await orderRepository.ChangeStatusAsync(command.OrderId, command.Status);
        
        if (order is null)
        {
            return Errors.Order.OrderNotFound(command.OrderId);
        }

        return mapper.Map<OrderResult>(order);
    }
}