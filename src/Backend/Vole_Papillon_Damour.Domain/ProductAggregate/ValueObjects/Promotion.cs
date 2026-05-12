using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

public sealed class Promotion : ValueObject
{
    public Promotion(int quantity, double discountedPrice)
    {
        Quantity = quantity;
        DiscountedPrice = discountedPrice;
    }

    public int Quantity { get; protected set; }
    public double DiscountedPrice { get; protected set; }
    
    public Promotion(){}
    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Quantity;
        yield return DiscountedPrice;
    }
}