using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;

public sealed class LinePartieId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static LinePartieId CreateUnique()
    {
        return new LinePartieId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static LinePartieId Create(Guid value)
    {
        return new LinePartieId(value);
    }
}