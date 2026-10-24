using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.Isbndb.Queries.SearchIsbndbEditions;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class SearchIsbndbEditionsQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReusesOneCachedSearchAcrossCaseAndAccentVariants()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var client = Substitute.For<IIsbndbClient>();
        client.SearchAsync("Les MISÉRABLES", IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>())
            .Returns(new IsbndbSearch([Notice("9782070363735", "Les Misérables")], 1));
        using var cacheServices = CreateCacheServices();
        var handler = CreateHandler(fixture, client, cacheServices);

        var first = await handler.Handle(new SearchIsbndbEditionsQuery("  Les MISÉRABLES "), CancellationToken.None);
        var second = await handler.Handle(new SearchIsbndbEditionsQuery("les miserables"), CancellationToken.None);

        first.Value.Items.Should().ContainSingle();
        first.Value.Items[0].Authors.Should().Be("Victor Hugo");
        first.Value.Items[0].PublicationYear.Should().Be(1862);
        first.Value.Items[0].Binding.Should().Be("Broché");
        second.Value.Items.Should().HaveCount(1);
        await client.Received(1).SearchAsync("Les MISÉRABLES", IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDailyPublicSearchLimitIsReached_ReturnsLimitWithoutCallingClient()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var usage = IsbndbDailyUsage.Create(DateOnly.FromDateTime(Now), IsbndbUsage.PublicSearch);
        usage.Increment(300);
        fixture.Context.IsbndbDailyUsages.Add(usage);
        await fixture.Context.SaveChangesAsync();
        var client = Substitute.For<IIsbndbClient>();
        using var cacheServices = CreateCacheServices();
        var handler = CreateHandler(fixture, client, cacheServices);

        var result = await handler.Handle(new SearchIsbndbEditionsQuery("dune"), CancellationToken.None);

        result.Value.Unavailable.Should().Be("limit");
        result.Value.Items.Should().BeEmpty();
        await client.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<IsbndbUsage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FiltersVisibleCatalogBooksInvalidIsbnsAndNonFrenchNotices()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var visibleIsbn).Should().BeTrue();
        var visible = Book.Create(visibleIsbn, Now.AddDays(-1));
        visible.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre visible", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-1),
            null);
        fixture.Context.Books.Add(visible);
        await fixture.Context.SaveChangesAsync();
        var client = Substitute.For<IIsbndbClient>();
        client.SearchAsync(Arg.Any<string>(), IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>())
            .Returns(new IsbndbSearch(
            [
                Notice("9782070363735", "Déjà au catalogue"),
                Notice("9782070409228", "Roman français"),
                Notice("not-an-isbn", "ISBN invalide"),
                Notice("9783140464079", "English book", "eng")
            ], 4));
        using var cacheServices = CreateCacheServices();
        var handler = CreateHandler(fixture, client, cacheServices);

        var result = await handler.Handle(new SearchIsbndbEditionsQuery("livre"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Isbn13.Should().Be("9782070409228");
        result.Value.Total.Should().Be(4);
    }

    [Fact]
    public async Task Handle_DoesNotCacheUnavailableResponses()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var client = Substitute.For<IIsbndbClient>();
        client.SearchAsync(Arg.Any<string>(), IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>())
            .Returns(new IsbndbSearch([], 0, IsbndbSearchStatus.Unavailable));
        using var cacheServices = CreateCacheServices();
        var handler = CreateHandler(fixture, client, cacheServices);

        (await handler.Handle(new SearchIsbndbEditionsQuery("dune"), CancellationToken.None)).Value.Unavailable
            .Should().Be("unavailable");
        (await handler.Handle(new SearchIsbndbEditionsQuery("dune"), CancellationToken.None)).Value.Unavailable
            .Should().Be("unavailable");

        await client.Received(2).SearchAsync(Arg.Any<string>(), IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("a very long query that is deliberately longer than two hundred characters a very long query that is deliberately longer than two hundred characters a very long query that is deliberately longer than two hundred characters a very long query that is deliberately longer than two hundred characters")]
    public async Task Handle_RejectsQueriesOutsideTheSupportedLength(string query)
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var client = Substitute.For<IIsbndbClient>();
        using var cacheServices = CreateCacheServices();
        var result = await CreateHandler(fixture, client, cacheServices)
            .Handle(new SearchIsbndbEditionsQuery(query), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Isbndb.SearchQueryInvalid");
    }

    [Fact]
    public async Task Handle_SingleCallerCancellationDoesNotCancelSharedFactoryWhileAnotherCallerIsWaiting()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var factoryStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishSearch = new TaskCompletionSource<IsbndbSearch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Substitute.For<IIsbndbClient>();
        client.SearchAsync(Arg.Any<string>(), IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();
                factoryStarted.TrySetResult(token);
                return finishSearch.Task.WaitAsync(token);
            });
        using var cacheServices = CreateCacheServices();
        var handler = CreateHandler(fixture, client, cacheServices);
        using var firstCancellation = new CancellationTokenSource();

        var first = handler.Handle(new SearchIsbndbEditionsQuery("dune"), firstCancellation.Token);
        var factoryToken = await factoryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = handler.Handle(new SearchIsbndbEditionsQuery("dune"), CancellationToken.None);
        await Task.Delay(25);
        firstCancellation.Cancel();
        await Task.Delay(25);

        factoryToken.IsCancellationRequested.Should().BeFalse();
        finishSearch.SetResult(new IsbndbSearch([Notice("9782070363735", "Dune")], 1));
        await second;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        await client.Received(1).SearchAsync(Arg.Any<string>(), IsbndbUsage.PublicSearch, Arg.Any<CancellationToken>());
    }

    private static SearchIsbndbEditionsQueryHandler CreateHandler(
        ScanBookFixture fixture,
        IIsbndbClient client,
        ServiceProvider cacheServices)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new SearchIsbndbEditionsQueryHandler(
            fixture.Context,
            client,
            cacheServices.GetRequiredService<HybridCache>(),
            clock,
            Options.Create(new IsbndbPublicSearchOptions()));
    }

    private static ServiceProvider CreateCacheServices()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider();
    }

    private static IsbndbBook Notice(string isbn, string title, string language = "fra") => new()
    {
        Isbn13 = isbn,
        Title = title,
        Language = language,
        Authors = ["Hugo, Victor"],
        Publisher = "Gallimard",
        DatePublished = "1862-04-03",
        Binding = "paperback",
        Image = "https://images.example/book.jpg"
    };
}
