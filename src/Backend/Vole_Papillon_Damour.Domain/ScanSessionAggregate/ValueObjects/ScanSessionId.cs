using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

public sealed class ScanSessionId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static ScanSessionId CreateUnique() => new(Guid.NewGuid());

    public static ScanSessionId Create(Guid value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
