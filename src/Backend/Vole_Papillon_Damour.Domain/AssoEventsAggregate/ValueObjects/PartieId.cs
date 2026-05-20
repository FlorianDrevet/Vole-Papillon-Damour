using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public sealed class PartieId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static PartieId CreateUnique()
    {
        return new PartieId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static PartieId Create(Guid value)
    {
        return new PartieId(value);
    }
}