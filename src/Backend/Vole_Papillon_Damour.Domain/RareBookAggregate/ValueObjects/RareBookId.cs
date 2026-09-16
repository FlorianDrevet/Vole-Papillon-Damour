using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

public sealed class RareBookId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static RareBookId CreateUnique() => new(Guid.NewGuid());

    public static RareBookId Create(Guid value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
