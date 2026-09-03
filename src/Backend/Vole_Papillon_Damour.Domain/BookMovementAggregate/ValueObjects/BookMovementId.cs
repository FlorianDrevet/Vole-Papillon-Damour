using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

public sealed class BookMovementId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static BookMovementId CreateUnique() => new(Guid.NewGuid());

    public static BookMovementId Create(Guid value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
