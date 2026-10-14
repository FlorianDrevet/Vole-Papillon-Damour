using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetQueue;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetRequester;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetStock;
using Vole_Papillon_Damour.Application.ManualRequests.Queries.GetSummary;
using Vole_Papillon_Damour.Application.tests.ManualRequests;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Queries;

public sealed class ManualRequestQueriesTests
{
    [Fact]
    public async Task Queue_FiltersOrdersAndSearchesByNormalizedPhone()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var alice = await fixture.AddRequesterAsync("Durand", "Alice", "06 12 34 56 78");
        var zoe = await fixture.AddRequesterAsync("Martin", "Zoé", "0600000000");
        var searchingOld = await fixture.AddEditionRequestAsync(alice.Id, "9782070363735", now.AddDays(-8));
        var searchingNew = await fixture.AddEditionRequestAsync(zoe.Id, "9783140464079", now.AddDays(-1));
        var foundOverdue = await fixture.AddEditionRequestAsync(alice.Id, "9791036377426", now.AddDays(-30));
        MarkFound(foundOverdue, now.AddDays(-15));
        var foundRecent = await fixture.AddEditionRequestAsync(zoe.Id, "9780306406157", now.AddDays(-10));
        MarkFound(foundRecent, now.AddDays(-2));
        var closedOld = await fixture.AddEditionRequestAsync(alice.Id, "9780306406157", now.AddDays(-20));
        closedOld.Cancel(ManualRequestFixture.AdministratorId, null, now.AddDays(-10));
        var closedRecent = await fixture.AddEditionRequestAsync(zoe.Id, "9780132350884", now.AddDays(-9));
        closedRecent.Cancel(ManualRequestFixture.AdministratorId, null, now.AddDays(-1));
        await fixture.Context.SaveChangesAsync();
        var handler = new GetManualRequestQueueQueryHandler(
            fixture.Context,
            fixture.CreateClock());

        var searching = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, null, 1, 20),
            CancellationToken.None);
        var phoneSearch = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, "06 12", 1, 20),
            CancellationToken.None);
        var found = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Found, null, 1, 20),
            CancellationToken.None);
        var closed = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Closed, null, 1, 20),
            CancellationToken.None);

        searching.IsError.Should().BeFalse();
        searching.Value.Items.Select(item => item.RequestId).Should().Equal(
            searchingOld.Id,
            searchingNew.Id);
        phoneSearch.IsError.Should().BeFalse();
        phoneSearch.Value.Items.Should().ContainSingle(item => item.RequesterId == alice.Id);
        found.IsError.Should().BeFalse();
        found.Value.Items.Select(item => item.RequestId).Should().Equal(foundOverdue.Id, foundRecent.Id);
        closed.IsError.Should().BeFalse();
        closed.Value.Items.Select(item => item.RequestId).Should().Equal(closedRecent.Id, closedOld.Id);
        searching.Value.FoundCount.Should().Be(2);
        searching.Value.SearchingCount.Should().Be(2);
        searching.Value.ClosedCount.Should().Be(2);
        searching.Value.FoundOverdueCount.Should().Be(1);
    }

    [Fact]
    public async Task FoundQueue_ReportsErasedRequesterAndRequesterDetailWithoutContact()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var requester = await fixture.AddRequesterAsync("Durand", "Alice", "0612345678");
        var request = await fixture.AddEditionRequestAsync(
            requester.Id,
            "9782070363735",
            now.AddDays(-30));
        MarkFound(request, now.AddDays(-15));
        requester.Erase(now.AddDays(-1));
        await fixture.Context.SaveChangesAsync();
        var clock = fixture.CreateClock();
        var queueHandler = new GetManualRequestQueueQueryHandler(fixture.Context, clock);
        var detailHandler = new GetManualRequesterQueryHandler(fixture.Context, clock);
        var summaryHandler = new GetManualRequestSummaryQueryHandler(fixture.Context, clock);

        var queue = await queueHandler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Found, null, 1, 20),
            CancellationToken.None);
        var detail = await detailHandler.Handle(
            new GetManualRequesterQuery(requester.Id),
            CancellationToken.None);
        var summary = await summaryHandler.Handle(new GetManualRequestSummaryQuery(), CancellationToken.None);

        queue.IsError.Should().BeFalse();
        queue.Value.Items.Should().ContainSingle(item =>
            item.RequesterErased &&
            item.RequesterLastName == null &&
            item.RequesterFirstName == null &&
            item.RequesterPhone == null);
        queue.Value.FoundOverdueCount.Should().Be(1);
        detail.IsError.Should().BeFalse();
        detail.Value.Erased.Should().BeTrue();
        detail.Value.LastName.Should().BeNull();
        detail.Value.FirstName.Should().BeNull();
        detail.Value.Phone.Should().BeNull();
        detail.Value.Requests.Should().ContainSingle(item => item.RequestId == request.Id);
        summary.IsError.Should().BeFalse();
        summary.Value.FoundCount.Should().Be(1);
        summary.Value.FoundOverdueCount.Should().Be(1);
    }

    [Fact]
    public async Task FoundQueue_ExposesReleasePlanAndWatcherCount()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(7)),
            new DateTimeOffset(now.AddDays(8)));
        var book = await fixture.AddBookAsync("9782070363735", workId: ManualRequestFixture.DefaultWorkId);
        await fixture.AddWatcherAsync(WatchlistItemScope.Work, workId: ManualRequestFixture.DefaultWorkId);
        var requester = await fixture.AddRequesterAsync();
        var request = await fixture.AddWorkRequestAsync(requester.Id);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        request.MarkFoundByScan(
            book.Id,
            session.Id,
            BookMovementId.CreateUnique(),
            session.VolunteerId,
            now.AddMinutes(-1));
        await fixture.Context.SaveChangesAsync();

        var result = await new GetManualRequestQueueQueryHandler(
            fixture.Context,
            fixture.CreateClock()).Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Found, null, 1, 20),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle(item =>
            item.RequestId == request.Id &&
            item.WatcherCount == 1 &&
            item.ReleaseMode == ManualRequestReleaseMode.NextFair &&
            item.ReleaseFairStart == fair.DateStart);
    }

    [Fact]
    public async Task StockQuery_ReturnsAvailableAndAnnouncedQuantities()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(7)),
            new DateTimeOffset(now.AddDays(8)));
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 2);
        await fixture.AddAnnouncementAsync("9782070363735", fair.Id, quantity: 3);

        var result = await new GetManualRequestStockQueryHandler(fixture.Context).Handle(
            new GetManualRequestStockQuery(
                ManualBookRequestScope.Edition,
                null,
                "9782070363735"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.QuantityAvailable.Should().Be(2);
        result.Value.QuantityAnnounced.Should().Be(3);
        result.Value.NextFairStart.Should().Be(fair.DateStart);
    }

    private static void MarkFound(ManualBookRequest request, DateTime foundAt)
    {
        request.MarkFoundFromStock(
            request.Isbn13!.Value,
            null,
            null,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.AdministratorId,
            foundAt);
    }
}
