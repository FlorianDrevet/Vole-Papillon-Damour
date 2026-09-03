using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.BookMovementAggregate;

public sealed class BookMovement : AggregateRoot<BookMovementId>
{
    public Isbn13 Isbn13 { get; private set; }
    public BookMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public bool ClockSuspect { get; private set; }
    public ScanSessionId? ScanSessionId { get; private set; }
    public UserId? VolunteerId { get; private set; }
    public AssoEventsId? AssoEventsId { get; private set; }
    public string? Note { get; private set; }
    public Guid? ClientGestureId { get; private set; }
    public BookMovementId? ReversalOfMovementId { get; private set; }

    private BookMovement(
        BookMovementId id,
        Isbn13 isbn13,
        BookMovementType type,
        int quantity,
        DateTime occurredAt,
        DateTime receivedAt,
        bool clockSuspect,
        ScanSessionId? scanSessionId,
        UserId? volunteerId,
        AssoEventsId? assoEventsId,
        string? note,
        Guid? clientGestureId,
        BookMovementId? reversalOfMovementId) : base(id)
    {
        EnsureIsbn(isbn13);
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "A book movement quantity cannot be zero.");
        }

        Isbn13 = isbn13;
        Type = type;
        Quantity = quantity;
        OccurredAt = DomainTime.RequireUtc(occurredAt, nameof(occurredAt));
        ReceivedAt = DomainTime.RequireUtc(receivedAt, nameof(receivedAt));
        ClockSuspect = clockSuspect;
        ScanSessionId = scanSessionId;
        VolunteerId = volunteerId;
        AssoEventsId = assoEventsId;
        Note = note;
        ClientGestureId = clientGestureId;
        ReversalOfMovementId = reversalOfMovementId;
    }

    public static BookMovement Create(
        BookMovementId id,
        Isbn13 isbn13,
        BookMovementType type,
        int quantity,
        DateTime occurredAt,
        DateTime receivedAt,
        bool clockSuspect,
        ScanSessionId? scanSessionId,
        UserId? volunteerId,
        AssoEventsId? assoEventsId,
        string? note,
        Guid? clientGestureId,
        BookMovementId? reversalOfMovementId = null)
    {
        return new BookMovement(
            id,
            isbn13,
            type,
            quantity,
            occurredAt,
            receivedAt,
            clockSuspect,
            scanSessionId,
            volunteerId,
            assoEventsId,
            note,
            clientGestureId,
            reversalOfMovementId);
    }

    public BookMovement()
    {
    }

    private static void EnsureIsbn(Isbn13 isbn13)
    {
        if (string.IsNullOrWhiteSpace(isbn13.Value))
        {
            throw new ArgumentException("A valid ISBN-13 is required.", nameof(isbn13));
        }
    }
}
