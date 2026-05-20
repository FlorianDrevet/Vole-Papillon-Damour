using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

public sealed class OrderId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static OrderId CreateUnique()
    {
        return new OrderId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static OrderId Create(Guid value)
    {
        return new OrderId(value);
    }
}