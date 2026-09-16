using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

public sealed class RareBookPhotoId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static RareBookPhotoId CreateUnique() => new(Guid.NewGuid());

    public static RareBookPhotoId Create(Guid value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
