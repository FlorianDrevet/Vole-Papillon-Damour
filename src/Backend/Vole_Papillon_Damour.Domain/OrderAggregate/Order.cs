using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.OrderAggregate.Entities;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate;

namespace Vole_Papillon_Damour.Domain.OrderAggregate;

public sealed class Order : AggregateRoot<OrderId>
{
    public string FamilyName { get; private set; } = null!;
    public StatusEnum Status { get; private set; } = null!;
    public double TotalPrice { get; private set; }
    public DateTime CreateDateTime { get; } = DateTime.Now;
    
    private List<OrderedProduct> _products = new List<OrderedProduct>();
    public IReadOnlyList<OrderedProduct> Products => _products.AsReadOnly();


    private Order(OrderId id, string familyName, StatusEnum status, double totalPrice, List<OrderedProduct> products)
        : base(id)
    {
        FamilyName = familyName;
        Status = status;
        TotalPrice = totalPrice;
        _products = products;
    }

    public static Order Create(string familyName, StatusEnum status, double totalPrice, List<OrderedProduct> products)
    {
        return new Order(OrderId.CreateUnique(), familyName, status, totalPrice, products);
    }

    public Order()
    {
    }

    public void ChangeStatus(StatusEnum status)
    {
        Status = status;
    }
}