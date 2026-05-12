using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

public sealed class OrderedProductId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static OrderedProductId CreateUnique()
    {
        return new OrderedProductId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static OrderedProductId Create(Guid value)
    {
        return new OrderedProductId(value);
    }
}