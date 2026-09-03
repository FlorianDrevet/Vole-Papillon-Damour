using FluentAssertions;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ScanSessionAggregateTests;

public sealed class ScanSessionTests
{
    [Fact]
    public void RecordScan_AndClose_UpdatesCountersAndLifecycle()
    {
        var volunteerId = UserId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var startedAt = new DateTime(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
        var session = ScanSession.Create(volunteerId, ScanMode.AvailableNow, null, startedAt);

        session.RecordScan(kept: true, startedAt.AddSeconds(2), startedAt.AddSeconds(3));
        session.RecordScan(kept: false, startedAt.AddSeconds(4), startedAt.AddSeconds(5));
        var closed = session.Close(ScanCloseReason.Manual, startedAt.AddMinutes(1));

        closed.Should().BeTrue();
        session.ScannedCount.Should().Be(2);
        session.KeptCount.Should().Be(1);
        session.RejectedCount.Should().Be(1);
        session.Status.Should().Be(ScanSessionStatus.Completed);
        session.EndedAt.Should().Be(startedAt.AddMinutes(1));
        session.CloseReason.Should().Be(ScanCloseReason.Manual);
    }

    [Fact]
    public void Close_WhenAlreadyCompleted_IsIdempotent()
    {
        var startedAt = new DateTime(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
        var session = ScanSession.Create(
            UserId.Create(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            ScanMode.NextFair,
            null,
            startedAt);

        session.Close(ScanCloseReason.Manual, startedAt.AddMinutes(1));
        var closedAgain = session.Close(ScanCloseReason.Inactivity, startedAt.AddMinutes(2));

        closedAgain.Should().BeFalse();
        session.CloseReason.Should().Be(ScanCloseReason.Manual);
        session.EndedAt.Should().Be(startedAt.AddMinutes(1));
    }
}
