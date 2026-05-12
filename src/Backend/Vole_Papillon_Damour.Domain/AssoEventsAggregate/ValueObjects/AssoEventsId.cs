using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

public sealed class AssoEventsId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static AssoEventsId CreateUnique()
    {
        return new AssoEventsId(Guid.NewGuid());
    }

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public static AssoEventsId Create(Guid value)
    {
        return new AssoEventsId(value);
    }
}