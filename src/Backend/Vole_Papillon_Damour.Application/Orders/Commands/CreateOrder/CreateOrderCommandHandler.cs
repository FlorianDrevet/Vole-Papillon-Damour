using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Orders.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate.Entities;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler(IOrderRepository orderRepository, IProductRepository productRepository, IMapper mapper)
    : IRequestHandler<CreateOrderCommand, ErrorOr<OrderResult>>
{
    public async Task<ErrorOr<OrderResult>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var products = command.OrderedProduct
            .Select(async p => await productRepository.GetByIdAsync(p.ProductId))
            .ToList();
        
        if (products.Contains(null))
        {
            return Errors.Order.OrderedProductNotFound();
        }

        var orderedProduct = products
            .Zip(command.OrderedProduct, (task, productCommand) =>  OrderedProduct.Create(productCommand.Quantity, task.Result!));

        var order = Order.Create(command.FamilyName,
            command.Status,
            command.TotalPrice,
            orderedProduct.ToList()
        );

        await orderRepository.AddAsync(order);

        return mapper.Map<OrderResult>(order);
    }
}