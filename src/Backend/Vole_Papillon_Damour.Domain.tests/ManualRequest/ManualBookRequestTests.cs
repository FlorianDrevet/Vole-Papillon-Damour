using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ManualRequest;

public sealed class ManualBookRequestTests
{
    private static readonly UserId Admin = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly UserId Volunteer = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000b2"));
    private static readonly DateTime RequestedAt = new(2026, 9, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FoundAt = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid RequesterId = Guid.Parse("00000000-0000-0000-0000-0000000000c3");
    private static readonly Isbn13 Gallimard = Parse("9782070612758");
    private static readonly Isbn13 Folio = Parse("9782070584628");

    [Fact]
    public void CreateWork_MatchesAnyEditionOfTheWork()
    {
        var request = WorkRequest();

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.Matches(Folio, "OL123W").Should().BeTrue();
        request.Matches(Folio, "OL999W").Should().BeFalse();
        request.Matches(Folio, null).Should().BeFalse();
    }

    [Fact]
    public void CreateEdition_MatchesOnlyItsIsbn()
    {
        var request = ManualBookRequest.CreateEdition(Guid.NewGuid(), RequesterId, Gallimard, "Le Petit Prince", null, null, null, RequestedAt);

        request.Matches(Gallimard, "OL123W").Should().BeTrue();
        request.Matches(Folio, "OL123W").Should().BeFalse();
    }

    [Fact]
    public void CreateWork_WithoutWorkId_Throws()
    {
        var act = () => ManualBookRequest.CreateWork(Guid.NewGuid(), RequesterId, " ", null, null, null, null, RequestedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFoundByScan_RecordsHoldAndIncrementsVersion()
    {
        var request = WorkRequest();
        var session = ScanSessionId.CreateUnique();
        var movement = BookMovementId.CreateUnique();

        request.MarkFoundByScan(Folio, session, movement, Volunteer, FoundAt);

        request.Status.Should().Be(ManualBookRequestStatus.Found);
        request.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.Scan);
        request.FoundIsbn13.Should().Be(Folio);
        request.FoundScanSessionId.Should().Be(session);
        request.HoldMovementId.Should().Be(movement);
        request.FoundBy.Should().Be(Volunteer);
        request.Version.Should().Be(1);
        request.RequestedAt.Should().Be(RequestedAt);
    }

    [Fact]
    public void MarkFoundFromStock_FromAnnouncement_RemembersTheFair()
    {
        var request = WorkRequest();
        var fair = AssoEventsId.Create(Guid.NewGuid());
        var announcementSession = ScanSessionId.CreateUnique();

        request.MarkFoundFromStock(Folio, fair, announcementSession, BookMovementId.CreateUnique(), Admin, FoundAt);

        request.FoundOrigin.Should().Be(ManualBookRequestFoundOrigin.StockAnnounced);
        request.PickedFromFairId.Should().Be(fair);
        request.FoundScanSessionId.Should().Be(announcementSession);
    }

    [Fact]
    public void MarkFoundFromStock_FairWithoutSession_Throws()
    {
        var request = WorkRequest();

        var act = () => request.MarkFoundFromStock(Folio, AssoEventsId.Create(Guid.NewGuid()), null, BookMovementId.CreateUnique(), Admin, FoundAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkFound_Twice_Throws()
    {
        var request = WorkRequest();
        request.MarkFoundByScan(Folio, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);

        var act = () => request.MarkFoundByScan(Gallimard, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkGiven_ClosesTheRequest()
    {
        var request = FoundRequest();

        request.MarkGiven(Admin, FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Given);
        request.IsOpen.Should().BeFalse();
        request.ClosedBy.Should().Be(Admin);
        request.ReleaseMovementId.Should().BeNull();
    }

    [Fact]
    public void Refuse_AndClose_KeepsReleaseMovement()
    {
        var request = FoundRequest();
        var release = BookMovementId.CreateUnique();

        request.Refuse(Admin, keepSearching: false, release, FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Refused);
        request.ReleaseMovementId.Should().Be(release);
    }

    [Fact]
    public void Refuse_AndKeepSearching_ReturnsToSearchingWithOriginalSeniority()
    {
        var request = FoundRequest();

        request.Refuse(Admin, keepSearching: true, BookMovementId.CreateUnique(), FoundAt.AddDays(1));

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.RequestedAt.Should().Be(RequestedAt);
        request.FoundIsbn13.Should().BeNull();
        request.HoldMovementId.Should().BeNull();
        request.ClosedAt.Should().BeNull();
    }

    [Fact]
    public void Cancel_FoundWithoutRelease_Throws()
    {
        var request = FoundRequest();

        var act = () => request.Cancel(Admin, releaseMovementId: null, FoundAt);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Cancel_Searching_ClosesWithoutRelease()
    {
        var request = WorkRequest();

        request.Cancel(Admin, releaseMovementId: null, FoundAt);

        request.Status.Should().Be(ManualBookRequestStatus.Cancelled);
    }

    [Fact]
    public void RevertScanHold_ReturnsToSearching()
    {
        var request = FoundRequest();

        request.RevertScanHold(FoundAt.AddMinutes(30));

        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        request.FoundScanSessionId.Should().BeNull();
        request.Version.Should().Be(2);
    }

    [Fact]
    public void RevertScanHold_OnStockPick_Throws()
    {
        var request = WorkRequest();
        request.MarkFoundFromStock(Folio, null, null, BookMovementId.CreateUnique(), Admin, FoundAt);

        var act = () => request.RevertScanHold(FoundAt);

        act.Should().Throw<InvalidOperationException>();
    }

    private static ManualBookRequest WorkRequest()
        => ManualBookRequest.CreateWork(Guid.NewGuid(), RequesterId, "OL123W", "Le Petit Prince", "Antoine de Saint-Exupéry", null, null, RequestedAt);

    private static ManualBookRequest FoundRequest()
    {
        var request = WorkRequest();
        request.MarkFoundByScan(Folio, ScanSessionId.CreateUnique(), BookMovementId.CreateUnique(), Volunteer, FoundAt);
        return request;
    }

    private static Isbn13 Parse(string value)
        => Isbn13.TryCreate(value, out var isbn) ? isbn : throw new InvalidOperationException(value);
}
