using Mapster;
using Vole_Papillon_Damour.Application.Orders.Commands.CreateOrder;
using Vole_Papillon_Damour.Application.Orders.Common;
using Vole_Papillon_Damour.Contracts.Order.Requests;
using Vole_Papillon_Damour.Contracts.Order.Responses;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate.Entities;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Common.Mapping;

public class OrderMappingConfig: IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<OrderedProduct, ProductOrderedResult>()
            .Map(dest => dest.ProductId, src => src.Product.Id);
        
        config.NewConfig<ProductOrderedResult, ProductOrderedResponse>()
            .Map(dest => dest.ProductId, src => src.ProductId.Value);

        config.NewConfig<OrderResult, OrderResponse>()
            .Map(dest => dest.OrderId, src => src.OrderId.Value)
            .Map(dest => dest.OrderedProduct, src => src.OrderedProduct)
            .Map(dest => dest.Status, src => src.Status.Value.ToString());
        
        config.NewConfig<CreateOrderRequests, CreateOrderCommand>()
            .Map(dest => dest.Status, src => StatusEnum.CreateFromString(src.Status))
            ;
        
        config.NewConfig<CreateOrderProductRequest, OrderedProductCommand>()
            .Map(dest => dest.ProductId, src => 
                new ProductId(src.ProductId));

        config.NewConfig<CreateOrderProductRequest, OrderedProductCommand>()
            .Map(dest => dest.ProductId, src => 
                new ProductId(src.ProductId));

        config.NewConfig<Order, OrderResult>()
            .Map(dest => dest.OrderedProduct, src => src.Products)
            .Map(dest => dest.OrderId, src => src.Id);
        
    }
}