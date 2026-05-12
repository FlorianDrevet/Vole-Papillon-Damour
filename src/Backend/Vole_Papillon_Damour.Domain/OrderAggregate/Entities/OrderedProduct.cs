using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate;

namespace Vole_Papillon_Damour.Domain.OrderAggregate.Entities;

public sealed class OrderedProduct : Entity<OrderedProductId>
{
    public int Quantity { get; protected set; }
    public Product Product { get; protected set; }
    private OrderedProduct(OrderedProductId id, int quantity, Product product)
        : base(id)
    {
        Quantity = quantity;
        this.Product = product;
    }

    public static OrderedProduct Create(int quantity, Product product)
    {
        return new OrderedProduct(OrderedProductId.CreateUnique(), quantity, product);
    }
    
    public OrderedProduct(){}
}