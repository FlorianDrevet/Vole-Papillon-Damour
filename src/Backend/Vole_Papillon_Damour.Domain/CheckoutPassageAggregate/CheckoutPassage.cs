using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;

public sealed class CheckoutPassage : Entity<Guid>
{
    public UserId? UserId { get; private set; }
    public CheckoutPassageStatus Status { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public UserId? AssociatedByVolunteerId { get; private set; }
    public DateTime? AssociatedAt { get; private set; }
    public string? UnresolvedReason { get; private set; }
    public DateTime? DissociatedAt { get; private set; }
    public UserId? DissociatedByUserId { get; private set; }
    public bool IsVisibleToMember => Status == CheckoutPassageStatus.Associated && UserId is not null;

    private CheckoutPassage(Guid id, DateTime occurredAt, DateTime createdAt) : base(EnsureId(id))
    {
        OccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
        CreatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
        Status = CheckoutPassageStatus.PendingAssociation;
    }

    public CheckoutPassage()
    {
    }

    public static CheckoutPassage OpenFromSale(Guid id, DateTime occurredAt, DateTime createdAt) =>
        new(id, occurredAt, createdAt);

    public static CheckoutPassage OpenFromAssociation(Guid id, DateTime occurredAt, DateTime createdAt) =>
        new(id, occurredAt, createdAt);

    public bool Associate(UserId userId, UserId volunteerId, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(volunteerId);

        if (Status == CheckoutPassageStatus.Dissociated)
        {
            return false;
        }

        if (Status == CheckoutPassageStatus.Associated)
        {
            return UserId == userId
                ? false
                : throw new InvalidOperationException("A validated passage cannot be moved to another member.");
        }

        UserId = userId;
        AssociatedByVolunteerId = volunteerId;
        AssociatedAt = DomainTime.RequireUtc(at, nameof(at));
        UnresolvedReason = null;
        Status = CheckoutPassageStatus.Associated;
        return true;
    }

    public void MarkUnresolved(string reason, DateTime at)
    {
        if (Status != CheckoutPassageStatus.PendingAssociation)
        {
            throw new InvalidOperationException("Only a pending checkout passage can be marked unresolved.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 64)
        {
            throw new ArgumentException("The unresolved reason cannot exceed 64 characters.", nameof(reason));
        }

        _ = DomainTime.RequireUtc(at, nameof(at));
        UnresolvedReason = normalizedReason;
        Status = CheckoutPassageStatus.Unresolved;
    }

    public void Dissociate(UserId byUserId, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(byUserId);
        UserId = null;
        DissociatedByUserId = byUserId;
        DissociatedAt = DomainTime.RequireUtc(at, nameof(at));
        Status = CheckoutPassageStatus.Dissociated;
    }

    public void Anonymize(DateTime at)
    {
        UserId = null;
        AssociatedByVolunteerId = null;
        DissociatedByUserId = null;
        DissociatedAt = DomainTime.RequireUtc(at, nameof(at));
        Status = CheckoutPassageStatus.Dissociated;
    }

    public void ForgetVolunteer(UserId volunteerId)
    {
        ArgumentNullException.ThrowIfNull(volunteerId);
        if (AssociatedByVolunteerId == volunteerId)
        {
            AssociatedByVolunteerId = null;
        }

        if (DissociatedByUserId == volunteerId)
        {
            DissociatedByUserId = null;
        }
    }

    private static Guid EnsureId(Guid id)
    {
        return id == Guid.Empty
            ? throw new ArgumentException("A checkout passage identifier is required.", nameof(id))
            : id;
    }
}
