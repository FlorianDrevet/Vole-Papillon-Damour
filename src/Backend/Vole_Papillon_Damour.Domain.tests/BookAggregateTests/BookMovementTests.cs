using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookMovementTests
{
    [Fact]
    public void Create_WithSignedQuantity_PreservesAuditAndReferences()
    {
        Isbn13.TryCreate("9782070363735", out var isbn).Should().BeTrue();
        var movementId = BookMovementId.CreateUnique();
        var sessionId = ScanSessionId.CreateUnique();
        var volunteerId = UserId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var eventId = AssoEventsId.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var occurredAt = new DateTime(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
        var receivedAt = occurredAt.AddMinutes(3);
        var clientGestureId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var movement = BookMovement.Create(
            movementId,
            isbn,
            BookMovementType.DirectEntry,
            1,
            occurredAt,
            receivedAt,
            clockSuspect: false,
            sessionId,
            volunteerId,
            eventId,
            "test",
            clientGestureId);

        movement.Id.Should().Be(movementId);
        movement.Quantity.Should().Be(1);
        movement.OccurredAt.Should().Be(occurredAt);
        movement.ReceivedAt.Should().Be(receivedAt);
        movement.ClientGestureId.Should().Be(clientGestureId);
        movement.ScanSessionId.Should().Be(sessionId);
        movement.VolunteerId.Should().Be(volunteerId);
        movement.AssoEventsId.Should().Be(eventId);
    }

    [Fact]
    public void Create_WithZeroQuantity_Throws()
    {
        Isbn13.TryCreate("9782070363735", out var isbn).Should().BeTrue();

        var action = () => BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn,
            BookMovementType.Rejection,
            0,
            UtcNow(),
            UtcNow(),
            false,
            null,
            null,
            null,
            null,
            null);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static DateTime UtcNow() => new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
}
