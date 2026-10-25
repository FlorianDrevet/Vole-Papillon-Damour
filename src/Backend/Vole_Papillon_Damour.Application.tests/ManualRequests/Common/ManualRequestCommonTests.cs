using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.ManualRequests.Common;

public sealed class ManualRequestCommonTests
{
    private static readonly DateTime Now = ManualRequestFixture.DefaultNow;
    private const string Isbn = "9782070612758";
    private const string OtherIsbn = "9782070584628";

    [Fact]
    public async Task FindOldestSearching_PrefersOldestAndIgnoresFound()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var alreadyFound = await fixture.AddWorkRequestAsync(requester.Id, "OL123W", Now.AddDays(-10));
        alreadyFound.MarkFoundByScan(
            ManualRequestFixture.ParseIsbn(Isbn), ScanSessionId.CreateUnique(),
            BookMovementId.CreateUnique(), ManualRequestFixture.VolunteerId, Now.AddDays(-1));
        await fixture.Context.SaveChangesAsync();
        var nextOldest = await fixture.AddWorkRequestAsync(requester.Id, "OL123W", Now.AddDays(-5));
        await fixture.AddWorkRequestAsync(requester.Id, "OL123W", Now.AddDays(-2));

        var result = await ManualRequestMatcher.FindOldestSearchingAsync(
            fixture.Context, ManualRequestFixture.ParseIsbn(Isbn), "OL123W", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(nextOldest.Id);
    }

    [Fact]
    public async Task FindOldestSearching_MatchesEditionOrWork()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var requester = await fixture.AddRequesterAsync();
        var workRequest = await fixture.AddWorkRequestAsync(requester.Id, "OL123W", Now.AddDays(-3));
        var editionRequest = await fixture.AddEditionRequestAsync(requester.Id, Isbn, Now.AddDays(-2));

        var workMatch = await ManualRequestMatcher.FindOldestSearchingAsync(
            fixture.Context, ManualRequestFixture.ParseIsbn(Isbn), "OL123W", CancellationToken.None);
        var editionMatch = await ManualRequestMatcher.FindOldestSearchingAsync(
            fixture.Context, ManualRequestFixture.ParseIsbn(Isbn), "OL999W", CancellationToken.None);

        workMatch!.Id.Should().Be(workRequest.Id);
        editionMatch!.Id.Should().Be(editionRequest.Id);
    }

    [Fact]
    public async Task ResolveWorkId_UsesBookThenResolver()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var bookIsbn = await fixture.AddBookAsync(Isbn, workId: "OL_FROM_BOOK");
        var resolverIsbn = await fixture.AddBookAsync(OtherIsbn);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveWorkIdAsync(resolverIsbn.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("OL_FROM_RESOLVER"));

        var fromBook = await ManualRequestMatcher.ResolveWorkIdAsync(
            fixture.Context, resolver, bookIsbn.Id, CancellationToken.None);
        var fromResolver = await ManualRequestMatcher.ResolveWorkIdAsync(
            fixture.Context, resolver, resolverIsbn.Id, CancellationToken.None);

        fromBook.Should().Be("OL_FROM_BOOK");
        fromResolver.Should().Be("OL_FROM_RESOLVER");
        await resolver.DidNotReceive().ResolveAsync(resolverIsbn.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stock_SumsEditionsOfTheWorkAndIgnoresUndatedAnnouncements()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        await fixture.AddBookAsync(Isbn, quantityAvailable: 2, workId: "OL123W");
        await fixture.AddBookAsync(OtherIsbn, quantityAvailable: 1, workId: "OL123W");
        var fair = await fixture.AddFairAsync(new DateTimeOffset(Now.AddDays(7)));
        await fixture.AddAnnouncementAsync(Isbn, fairId: null, quantity: 3);
        await fixture.AddAnnouncementAsync(OtherIsbn, fair.Id, quantity: 2);

        var stock = await ManualRequestStock.GetAsync(
            fixture.Context, ManualBookRequestScope.Work, "OL123W", null, CancellationToken.None);

        stock.QuantityAvailable.Should().Be(3);
        stock.QuantityAnnounced.Should().Be(2);
    }

    [Fact]
    public async Task Watchers_CountsActiveMembersOnly()
    {
        await using var fixture = await ManualRequestFixture.CreateAsync();
        var isbn = ManualRequestFixture.ParseIsbn(Isbn);
        await fixture.AddWatcherAsync(WatchlistItemScope.Work, "OL123W");
        await fixture.AddWatcherAsync(WatchlistItemScope.Edition, isbn: Isbn);
        await fixture.AddWatcherAsync(WatchlistItemScope.Work, "OL123W", suspended: true);

        var count = await ManualRequestWatchers.CountAsync(
            fixture.Context, isbn, "OL123W", CancellationToken.None);

        count.Should().Be(2);
    }
}
