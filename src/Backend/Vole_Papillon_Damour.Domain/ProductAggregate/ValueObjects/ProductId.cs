using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

public sealed class ProductId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static ProductId CreateUnique()
    {
        return new ProductId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static ProductId Create(Guid value)
    {
        return new ProductId(value);
    }
}