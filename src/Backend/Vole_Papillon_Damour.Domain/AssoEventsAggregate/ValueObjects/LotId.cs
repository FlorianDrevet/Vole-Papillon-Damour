using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public sealed class LotId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static LotId CreateUnique()
    {
        return new LotId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static LotId Create(Guid value)
    {
        return new LotId(value);
    }
}