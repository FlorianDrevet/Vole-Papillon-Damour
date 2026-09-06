using FluentAssertions;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests;

public sealed class BookAdministrationDomainTests
{
    private static readonly DateTime Now =
        new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Book_fair_revenue_is_optional_and_cannot_be_negative()
    {
        var fair = CreateFair();

        fair.SetBookRevenue(125.50m);

        fair.BookRevenue.Should().Be(125.50m);
        fair.SetBookRevenue(null);
        fair.BookRevenue.Should().BeNull();
        var act = () => fair.SetBookRevenue(-0.01m);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Announcement_quantity_correction_returns_delta_and_preserves_positive_quantity()
    {
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            ParseIsbn("9782070408504"),
            null,
            4,
            Now,
            ScanSessionId.CreateUnique());

        announcement.ApplyQuantityCorrection(7).Should().Be(3);
        announcement.Quantity.Should().Be(7);

        announcement.ApplyQuantityCorrection(2).Should().Be(-5);
        announcement.Quantity.Should().Be(2);

        var act = () => announcement.ApplyQuantityCorrection(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Scan_session_can_be_marked_resumed_after_an_administrative_correction()
    {
        var session = ScanSession.Create(
            UserId.CreateUnique(),
            ScanMode.AvailableNow,
            null,
            Now);
        session.Close(ScanCloseReason.Manual, Now.AddMinutes(10)).Should().BeTrue();

        session.MarkResumedAfterCorrection().Should().BeTrue();
        session.Status.Should().Be(ScanSessionStatus.Resumed);
        session.MarkResumedAfterCorrection().Should().BeFalse();
    }

    private static AssoEvents CreateFair()
    {
        var address = new Adresse(10, "Paris", "Rue du Test", 75001);
        return AssoEvents.Create(
            "Bourse de test",
            null,
            new EventsType(EventsType.EventsTypeEnum.Books),
            new DateTimeOffset(Now, TimeSpan.Zero),
            new DateTimeOffset(Now.AddDays(1), TimeSpan.Zero),
            null,
            null,
            null,
            address,
            null,
            [],
            "Test");
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
