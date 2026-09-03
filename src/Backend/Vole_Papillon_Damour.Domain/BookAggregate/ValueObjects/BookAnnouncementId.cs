using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

public sealed class BookAnnouncementId(Guid value) : ValueObject
{
    public Guid Value { get; protected set; } = value;

    public static BookAnnouncementId CreateUnique() => new(Guid.NewGuid());

    public static BookAnnouncementId Create(Guid value) => new(value);

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
