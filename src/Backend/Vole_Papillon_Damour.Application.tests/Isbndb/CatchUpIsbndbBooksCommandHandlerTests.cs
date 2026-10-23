using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class CatchUpIsbndbBooksCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenProcessingExistingBooks_UsesPriorityAndBackfillsWorkKeys()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var stock = CreateResolvedBook("9782070409228", Now.AddDays(-20), "Les Misérables", "Hugo, Victor");
        stock.RecordAvailableEntry(Now.AddDays(-1));
        var watched = CreateResolvedBook("9782070363735", Now.AddDays(-30), "Germinal", "Zola, Émile");
        var newest = CreateResolvedBook("9783140464079", Now.AddDays(-1), "L'Étranger", "Albert Camus");
        fixture.Context.Books.AddRange(stock, watched, newest);

        var userId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000123"));
        fixture.Context.Watchlists.Add(Watchlist.Create(userId, Now.AddDays(-40)));
        fixture.Context.WatchlistItems.Add(WatchlistItem.CreateEdition(
            Guid.Parse("00000000-0000-0000-0000-000000000124"),
            userId,
            watched.Id,
            Now.AddDays(-30)));
        await fixture.Context.SaveChangesAsync();

        fixture.Context.Entry(watched).Property(nameof(Book.WorkKey)).CurrentValue = null;
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var lookups = new RecordingLookupService(_ => new IsbndbLookup.NotFound());
        var handler = CreateHandler(fixture, lookups);

        var result = await handler.Handle(CreateCommand(batchSize: 1), CancellationToken.None);

        result.Absent.Should().Be(3);
        lookups.BulkCalls.SelectMany(call => call).Should().Equal(stock.Id, watched.Id, newest.Id);
        (await fixture.Context.Books.SingleAsync(book => book.Id == watched.Id)).WorkKey.Should().Be("GERMINAL|ZOLA");
        (await fixture.Context.IsbndbCatchUpRuns.SingleAsync()).StopReason.Should().Be(IsbndbCatchUpStopReason.Completed);
    }

    [Fact]
    public async Task Handle_WhenQuotaIsExhaustedOnSecondPage_PersistsFirstPageAndStops()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var first = CreateResolvedBook("9782070409228", Now.AddDays(-1), "Livre premier", "Auteur premier");
        var second = CreateResolvedBook("9782070363735", Now.AddDays(-2), "Livre second", "Auteur second");
        var third = CreateResolvedBook("9783140464079", Now.AddDays(-3), "Livre troisième", "Auteur troisième");
        fixture.Context.Books.AddRange(first, second, third);
        var recommendationJob = BookRecommendationJob.Create(first.Id.Value);
        recommendationJob.Request(Now.AddDays(-1));
        fixture.Context.BookRecommendationJobs.Add(recommendationJob);
        await fixture.Context.SaveChangesAsync();

        var lookups = new RecordingLookupService(
            _ => new IsbndbLookup.NotFound(),
            _ => new IsbndbLookup.QuotaExhausted(Now.AddDays(1)));
        var recommendationQueue = Substitute.For<IBookRecommendationWorkQueue>();
        var handler = CreateHandler(fixture, lookups, recommendationQueue: recommendationQueue);

        var result = await handler.Handle(CreateCommand(batchSize: 1), CancellationToken.None);

        result.StopReason.Should().Be(IsbndbCatchUpStopReason.QuotaExhausted);
        result.Remaining.Should().Be(2);
        lookups.BulkCalls.Should().HaveCount(2);
        (await fixture.Context.Books.SingleAsync(book => book.Id == first.Id)).IsAbsentFromIsbndb.Should().BeTrue();
        (await fixture.Context.Books.SingleAsync(book => book.Id == second.Id)).IsAbsentFromIsbndb.Should().BeFalse();
        (await fixture.Context.Books.SingleAsync(book => book.Id == third.Id)).IsAbsentFromIsbndb.Should().BeFalse();
        var run = await fixture.Context.IsbndbCatchUpRuns.SingleAsync();
        run.StopReason.Should().Be(IsbndbCatchUpStopReason.QuotaExhausted);
        run.Absent.Should().Be(1);
        (await fixture.Context.BookRecommendationJobs.SingleAsync()).RequestVersion.Should().Be(2);
        await recommendationQueue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items =>
                items.Single().Isbn13 == first.Id.Value && items.Single().RequestVersion == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIsbndbFindsBook_PreservesManualFieldsAndRelaunchesRecommendations()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = await fixture.AddBookAsync(isbn13.Value, quantityAvailable: 0);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                "Titre corrigé",
                "Auteur corrigé",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            Now.AddDays(-1));
        var job = BookRecommendationJob.Create(isbn13.Value);
        job.Request(Now.AddDays(-1));
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();

        var notice = new IsbndbBook
        {
            Isbn13 = isbn13.Value,
            Title = "Titre ISBNdb",
            Authors = ["Auteur ISBNdb"],
            Publisher = "Gallimard",
            DatePublished = "1995",
            RawJson = "{\"title\":\"Titre ISBNdb\"}"
        };
        var lookups = new RecordingLookupService(_ => new IsbndbLookup.Found(notice));
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveWorkIdAsync(isbn13, Arg.Any<CancellationToken>()).Returns((string?)null);
        var recommendationQueue = Substitute.For<IBookRecommendationWorkQueue>();
        var handler = CreateHandler(fixture, lookups, resolver, recommendationQueue);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Filled.Should().Be(1);
        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.Title.Should().Be("Titre corrigé");
        persisted.Authors.Should().Be("Auteur corrigé");
        persisted.Publisher.Should().Be("Gallimard");
        persisted.IsIsbndbSourced.Should().BeTrue();
        (await fixture.Context.BookIsbndbRecords.SingleAsync()).RawJson.Should().Be(notice.RawJson);
        var persistedJob = await fixture.Context.BookRecommendationJobs.SingleAsync();
        persistedJob.RequestVersion.Should().Be(2);
        await recommendationQueue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyCollection<BookRecommendationQueueItem>>(items =>
                items.Single().Isbn13 == isbn13.Value && items.Single().RequestVersion == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRecheckingStaleAbsentBooks_SkipsCompletedAndRecordsFreshCheck()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var staleIsbn).Should().BeTrue();
        Isbn13.TryCreate("9782070409228", out var completedIsbn).Should().BeTrue();
        var stale = Book.Create(staleIsbn, Now.AddDays(-60));
        stale.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre absent", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-60),
            null);
        stale.MarkAbsentFromIsbndb(Now.AddDays(-31));
        var completed = Book.Create(completedIsbn, Now.AddDays(-60));
        completed.ApplyAutomaticMetadata(
            new BookMetadataPatch("Livre complété", "Auteur", null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            Now.AddDays(-60),
            null);
        completed.MarkAbsentFromIsbndb(Now.AddDays(-31));
        completed.MarkIsbndbCompleted(
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000456")),
            Now.AddDays(-20));
        fixture.Context.Books.AddRange(stale, completed);
        await fixture.Context.SaveChangesAsync();

        var lookups = new RecordingLookupService(
            _ => new IsbndbLookup.NotFound(),
            isbn => new IsbndbLookup.NotFound());
        var handler = CreateHandler(fixture, lookups);

        var result = await handler.Handle(CreateCommand(), CancellationToken.None);

        result.Rechecked.Should().Be(1);
        lookups.SingleCalls.Should().Equal(staleIsbn);
        (await fixture.Context.Books.SingleAsync(book => book.Id == staleIsbn)).IsbndbCheckedAt.Should().Be(Now);
        (await fixture.Context.Books.SingleAsync(book => book.Id == completedIsbn)).IsbndbCheckedAt.Should().Be(Now.AddDays(-31));
    }

    [Fact]
    public async Task Handle_WhenCancelledDuringWorkKeyBackfill_SavesCancelledRun()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = CreateResolvedBook("9782070363735", Now.AddDays(-1), "Les Misérables", "Hugo, Victor");
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        var handler = CreateHandler(fixture, new RecordingLookupService(_ => new IsbndbLookup.NotFound()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => handler.Handle(CreateCommand(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await fixture.Context.IsbndbCatchUpRuns.SingleAsync()).StopReason.Should().Be(IsbndbCatchUpStopReason.Cancelled);
    }

    private static CatchUpIsbndbBooksCommandHandler CreateHandler(
        ScanBookFixture fixture,
        RecordingLookupService lookupService,
        IBibliographicMetadataResolver? resolver = null,
        IBookRecommendationWorkQueue? recommendationQueue = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new CatchUpIsbndbBooksCommandHandler(
            fixture.Context,
            lookupService,
            resolver ?? Substitute.For<IBibliographicMetadataResolver>(),
            clock,
            recommendationQueue);
    }

    private static CatchUpIsbndbBooksCommand CreateCommand(int batchSize = 100) =>
        new(BulkSize: batchSize, AbsentRecheckDays: 30);

    private static Book CreateResolvedBook(string isbn, DateTime firstSeenAt, string title, string authors)
    {
        Isbn13.TryCreate(isbn, out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, firstSeenAt);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(title, authors, null, null, null, null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            firstSeenAt,
            null);
        return book;
    }

    private sealed class RecordingLookupService(params Func<Isbn13, IsbndbLookup>[] results) : IIsbndbLookupService
    {
        private readonly Queue<Func<Isbn13, IsbndbLookup>> _results = new(results);

        public List<IReadOnlyList<Isbn13>> BulkCalls { get; } = [];
        public List<Isbn13> SingleCalls { get; } = [];

        public Task<IsbndbLookup> LookupAsync(Isbn13 isbn13, IsbndbUsage usage, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SingleCalls.Add(isbn13);
            return Task.FromResult(Next(isbn13));
        }

        public Task<IReadOnlyDictionary<Isbn13, IsbndbLookup>> LookupManyAsync(
            IReadOnlyList<Isbn13> isbn13s,
            IsbndbUsage usage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BulkCalls.Add(isbn13s.ToArray());
            IReadOnlyDictionary<Isbn13, IsbndbLookup> result = isbn13s.ToDictionary(isbn => isbn, Next);
            return Task.FromResult(result);
        }

        private IsbndbLookup Next(Isbn13 isbn13) =>
            _results.Count == 0
                ? new IsbndbLookup.NotFound()
                : _results.Dequeue()(isbn13);
    }
}
