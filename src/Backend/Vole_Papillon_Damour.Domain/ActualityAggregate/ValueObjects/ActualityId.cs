using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

public sealed class ActualityId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static ActualityId CreateUnique()
    {
        return new ActualityId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static ActualityId Create(Guid value)
    {
        return new ActualityId(value);
    }
}