using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Orders.Common;

namespace Vole_Papillon_Damour.Application.Orders.Queries.GetAllOrdersQuery;

public class GetAllOrdersQueryHandler(IOrderRepository orderRepository, IMapper mapper)
    : IRequestHandler<GetAllOrdersQuery, ErrorOr<List<OrderResult>>>
{
    public async Task<ErrorOr<List<OrderResult>>> Handle(GetAllOrdersQuery command, CancellationToken cancellationToken)
    {
        var orders = await orderRepository.GetAllAsync(order => order.Products);
        return orders.Select(mapper.Map<OrderResult>).ToList();
    }
}