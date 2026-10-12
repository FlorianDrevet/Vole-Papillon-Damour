using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Common;

public sealed class ManualRequestReleaseTests
{
    private const string Isbn = "9782070612758";

    [Fact]
    public async Task Plan_ScanAvailableNow_UsesAvailableNow()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync();
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.AvailableNow);
        plan.FairId.Should().BeNull();
        plan.FairStart.Should().BeNull();
    }

    [Fact]
    public async Task Plan_ScanNextFairWithFutureFair_UsesSessionFair()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.NextFair);
        plan.FairId.Should().Be(fair.Id);
        plan.FairStart.Should().Be(fair.DateStart);
    }

    [Theory]
    [InlineData("past")]
    [InlineData("cancelled")]
    [InlineData("missing")]
    public async Task Plan_ScanWithUnavailableFair_UsesAvailableNow(string fairState)
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = fairState == "missing"
            ? null
            : await fixture.AddFairAsync(fairState == "past"
                ? new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(-5))
                : new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        if (fairState == "cancelled")
        {
            fair!.Cancel();
            await fixture.Context.SaveChangesAsync();
        }

        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair?.Id);
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.AvailableNow);
        plan.FairId.Should().BeNull();
    }

    [Fact]
    public async Task Plan_StockAvailable_UsesAvailableNow()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var book = await fixture.AddBookAsync(Isbn);
        var request = await CreateRequestAsync(fixture);
        request.MarkFoundFromStock(
            book.Id, null, null, BookMovementId.CreateUnique(), ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.AvailableNow);
        plan.FairId.Should().BeNull();
        plan.FairStart.Should().BeNull();
    }

    [Fact]
    public async Task Plan_StockAnnouncedWithFutureFair_UsesPickedFair()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        await fixture.AddBookAsync(Isbn);
        var announcement = await fixture.AddAnnouncementAsync(Isbn, fair.Id, quantity: 2);
        var request = await CreateRequestAsync(fixture);
        request.MarkFoundFromStock(
            ManualRequestFixture.ParseIsbn(Isbn), fair.Id, announcement.ScanSessionId,
            BookMovementId.CreateUnique(), ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.NextFair);
        plan.FairId.Should().Be(fair.Id);
        plan.FairStart.Should().Be(fair.DateStart);
    }

    [Theory]
    [InlineData("past")]
    [InlineData("cancelled")]
    [InlineData("missing")]
    public async Task Plan_StockAnnouncementWithUnavailableFair_UsesAvailableNow(string fairState)
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = fairState == "missing"
            ? null
            : await fixture.AddFairAsync(fairState == "past"
                ? new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(-5))
                : new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        if (fairState == "cancelled")
        {
            fair!.Cancel();
            await fixture.Context.SaveChangesAsync();
        }

        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair?.Id);
        var request = await CreateRequestAsync(fixture);
        request.MarkFoundFromStock(
            ManualRequestFixture.ParseIsbn(Isbn), fair?.Id ?? AssoEventsId.CreateUnique(), session.Id,
            BookMovementId.CreateUnique(), ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.AvailableNow);
        plan.FairId.Should().BeNull();
    }

    [Fact]
    public async Task Release_AvailableNow_IncrementsStockAndCreatesDirectEntry()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync();
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var result = await ManualRequestRelease.ReleaseAsync(
            fixture.Context, request, ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow, CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id)).QuantityAvailable.Should().Be(1);
        var movement = fixture.Context.BookMovements.Local.Single(candidate => candidate.Id == result.Value);
        movement.Type.Should().Be(BookMovementType.DirectEntry);
        movement.Quantity.Should().Be(1);
        movement.OccurredAt.Should().Be(ManualRequestFixture.DefaultNow);
        movement.ScanSessionId.Should().BeNull();
        movement.VolunteerId.Should().Be(ManualRequestFixture.AdministratorId);
        movement.AssoEventsId.Should().BeNull();
        movement.Note.Should().Be("ManualRequest.Release");
    }

    [Fact]
    public async Task Release_NextFair_CreatesAnnouncementOnTheFair()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var result = await ManualRequestRelease.ReleaseAsync(
            fixture.Context, request, ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow, CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == book.Id)).QuantityAvailable.Should().Be(0);
        var announcement = fixture.Context.BookAnnouncements.Local.Single();
        announcement.Isbn13.Should().Be(book.Id);
        announcement.AssoEventsId.Should().Be(fair.Id);
        announcement.Quantity.Should().Be(1);
        announcement.ScanSessionId.Should().Be(session.Id);
        var movement = fixture.Context.BookMovements.Local.Single(candidate => candidate.Id == result.Value);
        movement.Type.Should().Be(BookMovementType.AnnouncementEntry);
        movement.Quantity.Should().Be(1);
        movement.ScanSessionId.Should().BeNull();
        movement.VolunteerId.Should().Be(ManualRequestFixture.AdministratorId);
        movement.AssoEventsId.Should().Be(fair.Id);
        movement.Note.Should().Be("ManualRequest.Release");
    }

    [Fact]
    public async Task Plan_ScanSessionReassignedToAvailableNow_UsesCurrentSessionMode()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(new DateTimeOffset(ManualRequestFixture.DefaultNow.AddDays(5)));
        var book = await fixture.AddBookAsync(Isbn);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        session.Close(ScanCloseReason.Manual, ManualRequestFixture.DefaultNow.AddMinutes(1));
        session.Reassign(ScanMode.AvailableNow, null).Should().BeTrue();
        await fixture.Context.SaveChangesAsync();
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, book.Id, session.Id);

        var plan = await ManualRequestRelease.PlanAsync(
            fixture.Context, request, ManualRequestFixture.DefaultNow, CancellationToken.None);

        plan.Mode.Should().Be(ManualRequestReleaseMode.AvailableNow);
    }

    [Fact]
    public async Task Release_FollowsRedirectToCanonicalBook()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        const string redirectedIsbn = "9782070612758";
        const string canonicalIsbn = "9782070612765";
        var redirected = await fixture.AddBookAsync(redirectedIsbn);
        var canonical = await fixture.AddBookAsync(canonicalIsbn);
        redirected.RedirectTo(canonical.Id);
        await fixture.Context.SaveChangesAsync();
        var session = await fixture.AddSessionAsync();
        var request = await CreateRequestAsync(fixture);
        MarkFoundByScan(request, redirected.Id, session.Id);

        var result = await ManualRequestRelease.ReleaseAsync(
            fixture.Context, request, ManualRequestFixture.AdministratorId,
            ManualRequestFixture.DefaultNow, CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == canonical.Id)).QuantityAvailable.Should().Be(1);
        (await fixture.Context.Books.SingleAsync(candidate => candidate.Id == redirected.Id)).QuantityAvailable.Should().Be(0);
        fixture.Context.BookMovements.Local.Single(candidate => candidate.Id == result.Value).Isbn13.Should().Be(canonical.Id);
    }

    private static async Task<ManualBookRequest> CreateRequestAsync(
        ManualRequestFixture fixture)
    {
        var requester = await fixture.AddRequesterAsync();
        return await fixture.AddEditionRequestAsync(requester.Id, Isbn);
    }

    private static void MarkFoundByScan(
        ManualBookRequest request,
        Isbn13 isbn13,
        ScanSessionId scanSessionId)
    {
        request.MarkFoundByScan(
            isbn13,
            scanSessionId,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.VolunteerId,
            ManualRequestFixture.DefaultNow);
    }
}
