using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookAggregate.Entities;

public sealed class BookAnnouncement : Entity<BookAnnouncementId>
{
    public Isbn13 Isbn13 { get; private set; }
    public AssoEventsId? AssoEventsId { get; private set; }
    public int Quantity { get; private set; }
    public BookAnnouncementStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public ScanSessionId ScanSessionId { get; private set; } = null!;
    public Guid? ClientGestureId { get; private set; }

    private BookAnnouncement(
        BookAnnouncementId id,
        Isbn13 isbn13,
        AssoEventsId? assoEventsId,
        int quantity,
        DateTime createdAt,
        ScanSessionId scanSessionId,
        Guid? clientGestureId) : base(id)
    {
        EnsureIsbn(isbn13);
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "An announcement quantity must be positive.");
        }

        Isbn13 = isbn13;
        AssoEventsId = assoEventsId;
        Quantity = quantity;
        Status = BookAnnouncementStatus.Announced;
        CreatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
        ScanSessionId = scanSessionId ?? throw new ArgumentNullException(nameof(scanSessionId));
        ClientGestureId = clientGestureId;
    }

    public static BookAnnouncement Create(
        BookAnnouncementId id,
        Isbn13 isbn13,
        AssoEventsId? assoEventsId,
        int quantity,
        DateTime createdAt,
        ScanSessionId scanSessionId,
        Guid? clientGestureId = null)
    {
        return new BookAnnouncement(
            id,
            isbn13,
            assoEventsId,
            quantity,
            createdAt,
            scanSessionId,
            clientGestureId);
    }

    public BookAnnouncement()
    {
    }

    public bool Release(DateTime releasedAt)
    {
        if (Status != BookAnnouncementStatus.Announced)
        {
            return false;
        }

        ReleasedAt = DomainTime.RequireUtc(releasedAt, nameof(releasedAt));
        Status = BookAnnouncementStatus.Released;
        return true;
    }

    public bool Cancel()
    {
        if (Status != BookAnnouncementStatus.Announced)
        {
            return false;
        }

        Status = BookAnnouncementStatus.Cancelled;
        return true;
    }

    public bool AttachTo(AssoEventsId assoEventsId)
    {
        ArgumentNullException.ThrowIfNull(assoEventsId);

        if (Status != BookAnnouncementStatus.Announced || AssoEventsId is not null)
        {
            return false;
        }

        AssoEventsId = assoEventsId;
        return true;
    }

    public bool DetachFromFair()
    {
        if (Status != BookAnnouncementStatus.Announced || AssoEventsId is null)
        {
            return false;
        }

        AssoEventsId = null;
        return true;
    }

    private static void EnsureIsbn(Isbn13 isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }
    }
}
