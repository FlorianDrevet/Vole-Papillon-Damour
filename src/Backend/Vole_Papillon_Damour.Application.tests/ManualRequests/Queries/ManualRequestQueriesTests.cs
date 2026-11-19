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
    public async Task Queue_PagesFilteredRequestsAndCountsEveryTab()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var requester = await fixture.AddRequesterAsync();
        string[] isbns = ["9782070363735", "9783140464079", "9791036377426", "9780306406157", "9780132350884"];
        var searching = new List<ManualBookRequest>();
        for (var index = 0; index < isbns.Length; index++)
        {
            searching.Add(await fixture.AddEditionRequestAsync(requester.Id, isbns[index], now.AddDays(-10 + index)));
        }

        var found = await fixture.AddEditionRequestAsync(requester.Id, "9780306406157", now.AddDays(-20));
        MarkFound(found, now.AddDays(-1));
        await fixture.Context.SaveChangesAsync();
        var handler = new GetManualRequestQueueQueryHandler(fixture.Context, fixture.CreateClock());

        var secondPage = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, null, 2, 2),
            CancellationToken.None);

        secondPage.IsError.Should().BeFalse();
        secondPage.Value.Items.Select(item => item.RequestId).Should().Equal(searching[2].Id, searching[3].Id);
        secondPage.Value.TotalCount.Should().Be(5);
        secondPage.Value.SearchingCount.Should().Be(5);
        secondPage.Value.FoundCount.Should().Be(1);
        secondPage.Value.ClosedCount.Should().Be(0);
        secondPage.Value.Page.Should().Be(2);
        secondPage.Value.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Queue_SearchTreatsLikeWildcardsAsLiteralsAndIgnoresCase()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var requester = await fixture.AddRequesterAsync("Durand", "Alice", "0612345678");
        var underscore = await fixture.AddEditionRequestAsync(
            requester.Id, "9782070363735", now.AddDays(-3), "Tout_va bien");
        var percent = await fixture.AddEditionRequestAsync(
            requester.Id, "9783140464079", now.AddDays(-2), "A 50% off");
        var other = await fixture.AddEditionRequestAsync(
            requester.Id, "9791036377426", now.AddDays(-1), "Autre livre");
        await fixture.Context.SaveChangesAsync();
        var handler = new GetManualRequestQueueQueryHandler(fixture.Context, fixture.CreateClock());

        var underscoreSearch = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, "_", 1, 20),
            CancellationToken.None);
        var percentSearch = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, "%", 1, 20),
            CancellationToken.None);
        var nameSearch = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, "DURAND", 1, 20),
            CancellationToken.None);

        underscoreSearch.IsError.Should().BeFalse();
        underscoreSearch.Value.Items.Select(item => item.RequestId).Should().Equal(underscore.Id);
        underscoreSearch.Value.TotalCount.Should().Be(1);
        percentSearch.IsError.Should().BeFalse();
        percentSearch.Value.Items.Select(item => item.RequestId).Should().Equal(percent.Id);
        nameSearch.IsError.Should().BeFalse();
        nameSearch.Value.Items.Select(item => item.RequestId).Should().Equal(underscore.Id, percent.Id, other.Id);
        nameSearch.Value.TotalCount.Should().Be(3);
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
            book.Id.AsIsbn13(),
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
    public async Task Queues_ResolveStockWatchersAndReleasePlanPerRequest()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var now = ManualRequestFixture.DefaultNow;
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(7)),
            new DateTimeOffset(now.AddDays(8)));
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 2, workId: ManualRequestFixture.DefaultWorkId);
        await fixture.AddBookAsync("9783140464079", quantityAvailable: 1, workId: ManualRequestFixture.DefaultWorkId);
        await fixture.AddAnnouncementAsync("9783140464079", fair.Id, quantity: 3);
        var requester = await fixture.AddRequesterAsync();
        var workSearch = await fixture.AddWorkRequestAsync(requester.Id);
        var editionSearch = await fixture.AddEditionRequestAsync(requester.Id, "9791036377426", now.AddDays(-1));
        var workFound = await fixture.AddWorkRequestAsync(requester.Id);
        var scanSession = await fixture.AddSessionAsync(ScanMode.NextFair, fair.Id);
        workFound.MarkFoundByScan(
            ManualRequestFixture.ParseIsbn("9782070363735"),
            scanSession.Id,
            BookMovementId.CreateUnique(),
            scanSession.VolunteerId,
            now.AddDays(-15));
        var editionFound = await fixture.AddEditionRequestAsync(requester.Id, "9780306406157", now.AddDays(-30));
        editionFound.MarkFoundFromStock(
            ManualRequestFixture.ParseIsbn("9780306406157"),
            fair.Id,
            scanSession.Id,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.AdministratorId,
            now.AddDays(-2));
        var availableSession = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var scanAvailable = await fixture.AddEditionRequestAsync(requester.Id, "9780132350884", now.AddDays(-40));
        scanAvailable.MarkFoundByScan(
            ManualRequestFixture.ParseIsbn("9780132350884"),
            availableSession.Id,
            BookMovementId.CreateUnique(),
            availableSession.VolunteerId,
            now.AddDays(-1));
        await fixture.AddWatcherAsync(WatchlistItemScope.Work, workId: ManualRequestFixture.DefaultWorkId);
        await fixture.AddWatcherAsync(WatchlistItemScope.Edition, isbn: "9782070363735");
        await fixture.AddWatcherAsync(WatchlistItemScope.Edition, isbn: "9780306406157");
        await fixture.AddWatcherAsync(WatchlistItemScope.Edition, isbn: "9780306406157", suspended: true);
        await fixture.Context.SaveChangesAsync();
        var handler = new GetManualRequestQueueQueryHandler(fixture.Context, fixture.CreateClock());

        var searching = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Searching, null, 1, 20),
            CancellationToken.None);
        var found = await handler.Handle(
            new GetManualRequestQueueQuery(ManualRequestTab.Found, null, 1, 20),
            CancellationToken.None);

        searching.IsError.Should().BeFalse();
        searching.Value.Items.Select(item => item.RequestId).Should().Equal(editionSearch.Id, workSearch.Id);
        var workItem = searching.Value.Items.Single(item => item.RequestId == workSearch.Id);
        workItem.QuantityAvailable.Should().Be(3);
        workItem.QuantityAnnounced.Should().Be(3);
        var editionItem = searching.Value.Items.Single(item => item.RequestId == editionSearch.Id);
        editionItem.QuantityAvailable.Should().Be(0);
        editionItem.QuantityAnnounced.Should().Be(0);
        found.IsError.Should().BeFalse();
        found.Value.Items.Select(item => item.RequestId).Should().Equal(workFound.Id, editionFound.Id, scanAvailable.Id);
        found.Value.Items[0].WatcherCount.Should().Be(2);
        found.Value.Items[0].ReleaseMode.Should().Be(ManualRequestReleaseMode.NextFair);
        found.Value.Items[0].ReleaseFairStart.Should().Be(fair.DateStart);
        found.Value.Items[1].WatcherCount.Should().Be(1);
        found.Value.Items[1].ReleaseMode.Should().Be(ManualRequestReleaseMode.NextFair);
        found.Value.Items[1].ReleaseFairStart.Should().Be(fair.DateStart);
        found.Value.Items[2].WatcherCount.Should().Be(0);
        found.Value.Items[2].ReleaseMode.Should().Be(ManualRequestReleaseMode.AvailableNow);
        found.Value.Items[2].ReleaseFairStart.Should().BeNull();
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
            request.Isbn13!.Value.AsIsbn13(),
            null,
            null,
            BookMovementId.CreateUnique(),
            ManualRequestFixture.AdministratorId,
            foundAt);
    }
}
