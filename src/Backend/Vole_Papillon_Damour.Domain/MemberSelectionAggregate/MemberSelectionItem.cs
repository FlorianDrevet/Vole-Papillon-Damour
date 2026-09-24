using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.MemberSelectionAggregate;

public sealed class MemberSelectionItem : Entity<Guid>
{
    public const int MaxItemsPerMember = 500;

    public UserId UserId { get; private set; } = null!;
    public Isbn13? Isbn13 { get; private set; }
    public RareBookId? RareBookId { get; private set; }
    public MemberSelectionStatus Status { get; private set; }
    public DateTime AddedAt { get; private set; }
    public DateTime StatusChangedAt { get; private set; }
    public DateTime? PurchasedAt { get; private set; }

    private MemberSelectionItem(
        Guid id,
        UserId userId,
        Isbn13? isbn13,
        RareBookId? rareBookId,
        DateTime addedAt) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A selection item identifier is required.", nameof(id));
        }

        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        if ((isbn13 is null) == (rareBookId is null))
        {
            throw new ArgumentException("A selection item targets exactly one edition or one rare book.");
        }

        UserId = userId;
        Isbn13 = isbn13;
        RareBookId = rareBookId;
        Status = MemberSelectionStatus.ToTake;
        AddedAt = DomainTime.RequireUtc(addedAt, nameof(addedAt));
        StatusChangedAt = AddedAt;
    }

    public static MemberSelectionItem CreateForEdition(Guid id, UserId userId, Isbn13 isbn13, DateTime addedAt)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }

        return new MemberSelectionItem(id, userId, isbn13, null, addedAt);
    }

    public static MemberSelectionItem CreateForRareBook(Guid id, UserId userId, RareBookId rareBookId, DateTime addedAt)
    {
        if (rareBookId is null || rareBookId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid rare book identifier is required.", nameof(rareBookId));
        }

        return new MemberSelectionItem(id, userId, null, rareBookId, addedAt);
    }

    public MemberSelectionItem()
    {
    }

    public void ChangeStatus(MemberSelectionStatus status, DateTime changedAt)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown selection status.");
        }

        var utcChangedAt = DomainTime.RequireUtc(changedAt, nameof(changedAt));
        PurchasedAt = status == MemberSelectionStatus.Purchased
            ? PurchasedAt ?? utcChangedAt
            : null;
        Status = status;
        StatusChangedAt = utcChangedAt;
    }

    public bool MarkPurchased(DateTime purchasedAt)
    {
        if (Status == MemberSelectionStatus.Purchased)
        {
            return false;
        }

        var utcPurchasedAt = DomainTime.RequireUtc(purchasedAt, nameof(purchasedAt));
        Status = MemberSelectionStatus.Purchased;
        PurchasedAt = utcPurchasedAt;
        StatusChangedAt = utcPurchasedAt;
        return true;
    }
}
