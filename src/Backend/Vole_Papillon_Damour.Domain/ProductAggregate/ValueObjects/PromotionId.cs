using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

public sealed class PromotionId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static PromotionId CreateUnique()
    {
        return new PromotionId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static PromotionId Create(Guid value)
    {
        return new PromotionId(value);
    }
}