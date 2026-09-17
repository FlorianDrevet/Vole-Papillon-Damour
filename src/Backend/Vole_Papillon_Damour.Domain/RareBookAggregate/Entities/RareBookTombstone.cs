using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RareBookAggregate.Entities;

/// <summary>
/// Retains the deletion point long enough for offline catalogues to remove a rare copy.
/// It deliberately has no foreign key: the referenced RareBook row is already gone.
/// </summary>
public sealed class RareBookTombstone
{
    public Guid RareBookId { get; private set; }
    public DateTime DeletedAt { get; private set; }

    private RareBookTombstone()
    {
    }

    private RareBookTombstone(RareBookId rareBookId, DateTime deletedAt)
    {
        if (rareBookId.Value == Guid.Empty)
        {
            throw new ArgumentException("A rare book identifier is required.", nameof(rareBookId));
        }

        if (deletedAt.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The deletion timestamp must be expressed in UTC.", nameof(deletedAt));
        }

        RareBookId = rareBookId.Value;
        DeletedAt = deletedAt;
    }

    public static RareBookTombstone Create(RareBookId rareBookId, DateTime deletedAt)
    {
        return new RareBookTombstone(rareBookId, deletedAt);
    }
}
