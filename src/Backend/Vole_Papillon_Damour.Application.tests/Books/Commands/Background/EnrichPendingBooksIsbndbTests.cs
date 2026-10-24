using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Background;

public sealed class EnrichPendingBooksIsbndbTests
{
    private static readonly DateTime WorkerNow = new(2026, 9, 3, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenIsbndbFindsBook_AppliesNoticeAndPersistsRawRecord()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbn = ParseIsbn("9782070409228");
        await fixture.AddBookAsync(isbn.Value, quantityAvailable: 0);
        var notice = new IsbndbBook
        {
            Isbn13 = isbn.Value,
            Title = "Les Misérables",
            TitleLong = "Les Misérables, tome 1",
            Authors = ["Hugo, Victor"],
            Publisher = "Gallimard",
            DatePublished = "1995",
            Binding = "Paperback",
            Language = "fra",
            Pages = 512,
            Edition = "Folio classique",
            Subjects = ["Histoire"],
            Image = "https://images.isbndb.com/covers/book.jpg",
            RawJson = "{\"title\":\"Les Misérables\"}"
        };
        var lookup = new IsbndbLookup.Found(notice);
        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupManyAsync(Arg.Any<IReadOnlyList<Isbn13>>(), IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Isbn13, IsbndbLookup> { [isbn] = lookup });
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn, Arg.Any<CancellationToken>(), false, IsbndbUsage.Scan, lookup)
            .Returns(CreateIsbndbMetadata(isbn, notice));
        resolver.ResolveWorkIdAsync(isbn, Arg.Any<CancellationToken>()).Returns("bnf:work-1");
        var clock = CreateClock();
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, clock, null, lookups);

        await handler.Handle(
            new EnrichPendingBooksCommand(BatchSize: 1, Isbn13s: [isbn]),
            CancellationToken.None);

        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsIsbndbSourced.Should().BeTrue();
        persisted.IsAbsentFromIsbndb.Should().BeFalse();
        persisted.MetadataSource.Should().Be(BookMetadataSource.Isbndb);
        persisted.Title.Should().Be("Les Misérables");
        persisted.Authors.Should().Be("Victor Hugo");
        persisted.Pages.Should().Be(512);
        persisted.Edition.Should().Be("Folio classique");
        persisted.WorkId.Should().Be("bnf:work-1");
        persisted.WorkKey.Should().Be("MISERABLES|HUGO");
        persisted.CoverSource.Should().Be(BookCoverSource.Isbndb);
        var record = await fixture.Context.BookIsbndbRecords.SingleAsync();
        record.TitleLong.Should().Be("Les Misérables, tome 1");
        record.RawJson.Should().Be(notice.RawJson);
        await lookups.Received(1).LookupManyAsync(
            Arg.Is<IReadOnlyList<Isbn13>>(values => values.SequenceEqual(new[] { isbn })),
            IsbndbUsage.Scan,
            Arg.Any<CancellationToken>());
        await resolver.Received(1).ResolveWorkIdAsync(isbn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIsbndbReportsNotFound_AppliesFallbackAndMarksBookAbsent()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbn = ParseIsbn("9782070409228");
        await fixture.AddBookAsync(isbn.Value, quantityAvailable: 0);
        var lookup = new IsbndbLookup.NotFound();
        var lookups = CreateLookupService(isbn, lookup);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn, Arg.Any<CancellationToken>(), false, IsbndbUsage.Scan, lookup)
            .Returns(CreateFallbackMetadata(isbn));
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, CreateClock(), null, lookups);

        await handler.Handle(
            new EnrichPendingBooksCommand(BatchSize: 1, Isbn13s: [isbn]),
            CancellationToken.None);

        var persisted = await fixture.Context.Books.SingleAsync();
        persisted.IsIsbndbSourced.Should().BeFalse();
        persisted.IsAbsentFromIsbndb.Should().BeTrue();
        persisted.Title.Should().Be("Livre de secours");
        persisted.MetadataSource.Should().Be(BookMetadataSource.Bnf);
    }

    [Fact]
    public async Task Handle_WhenFallbackFailsAfterIsbndbNotFound_MarksBookAbsentAndRelaunchesRecommendations()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbn = ParseIsbn("9782070409228");
        await fixture.AddBookAsync(isbn.Value, quantityAvailable: 0);
        fixture.Context.BookRecommendationJobs.Add(BookRecommendationJob.Create(isbn.Value));
        await fixture.Context.SaveChangesAsync();
        var lookup = new IsbndbLookup.NotFound();
        var lookups = CreateLookupService(isbn, lookup);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn, Arg.Any<CancellationToken>(), false, IsbndbUsage.Scan, lookup)
            .Returns(_ => Task.FromException<BookMetadataResult?>(
                new HttpRequestException("Fallback providers are unavailable.")));
        var recommendationQueue = Substitute.For<IBookRecommendationWorkQueue>();
        var handler = new EnrichPendingBooksCommandHandler(
            fixture.Context,
            resolver,
            CreateClock(),
            recommendationQueue,
            lookups);

        await handler.Handle(
            new EnrichPendingBooksCommand(BatchSize: 1, Isbn13s: [isbn]),
            CancellationToken.None);

        var persisted = await fixture.Context.Books.SingleAsync();
        var recommendationJob = await fixture.Context.BookRecommendationJobs.SingleAsync();
        persisted.IsAbsentFromIsbndb.Should().BeTrue();
        persisted.IsIsbndbSourced.Should().BeFalse();
        persisted.MetadataStatus.Should().Be(BookMetadataStatus.Failed);
        recommendationJob.RequestVersion.Should().Be(1);
        await recommendationQueue.Received(1).EnqueueBooksAsync(
            Arg.Is<IReadOnlyList<BookRecommendationQueueItem>>(items =>
                items.Count == 1 && items[0].Isbn13 == isbn.Value && items[0].RequestVersion == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIsbndbIsDeferred_LeavesMarkersFalseAndDoesNotRelaunchRecommendations()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbn = ParseIsbn("9782070409228");
        await fixture.AddBookAsync(isbn.Value, quantityAvailable: 0);
        var job = BookRecommendationJob.Create(isbn.Value);
        fixture.Context.BookRecommendationJobs.Add(job);
        await fixture.Context.SaveChangesAsync();
        var lookup = new IsbndbLookup.QuotaExhausted(WorkerNow.AddHours(4));
        var lookups = CreateLookupService(isbn, lookup);
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn, Arg.Any<CancellationToken>(), false, IsbndbUsage.Scan, lookup)
            .Returns(CreateFallbackMetadata(isbn));
        var recommendationQueue = Substitute.For<IBookRecommendationWorkQueue>();
        var handler = new EnrichPendingBooksCommandHandler(
            fixture.Context,
            resolver,
            CreateClock(),
            recommendationQueue,
            lookups);

        await handler.Handle(
            new EnrichPendingBooksCommand(BatchSize: 1, Isbn13s: [isbn]),
            CancellationToken.None);

        var persisted = await fixture.Context.Books.SingleAsync();
        var persistedJob = await fixture.Context.BookRecommendationJobs.SingleAsync();
        persisted.IsIsbndbSourced.Should().BeFalse();
        persisted.IsAbsentFromIsbndb.Should().BeFalse();
        persistedJob.RequestVersion.Should().Be(0);
        await recommendationQueue.DidNotReceiveWithAnyArgs().EnqueueBooksAsync(default!, default);
    }

    [Fact]
    public async Task Handle_WithThreeCandidates_LooksUpTheBatchOnce()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbns = new[]
        {
            ParseIsbn("9782070409228"),
            ParseIsbn("9782070612758"),
            ParseIsbn("9782253004226")
        };
        foreach (var isbn in isbns)
        {
            await fixture.AddBookAsync(isbn.Value, quantityAvailable: 0);
        }

        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupManyAsync(Arg.Any<IReadOnlyList<Isbn13>>(), IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var requested = call.ArgAt<IReadOnlyList<Isbn13>>(0);
                return requested.ToDictionary(isbn => isbn, _ => (IsbndbLookup)new IsbndbLookup.QuotaExhausted(null));
            });
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(
                Arg.Any<Isbn13>(),
                Arg.Any<CancellationToken>(),
                false,
                IsbndbUsage.Scan,
                Arg.Any<IsbndbLookup?>())
            .Returns((BookMetadataResult?)null);
        var handler = new EnrichPendingBooksCommandHandler(fixture.Context, resolver, CreateClock(), null, lookups);

        await handler.Handle(new EnrichPendingBooksCommand(BatchSize: 3), CancellationToken.None);

        await lookups.Received(1).LookupManyAsync(
            Arg.Is<IReadOnlyList<Isbn13>>(values => isbns.All(values.Contains)),
            IsbndbUsage.Scan,
            Arg.Any<CancellationToken>());
        await resolver.Received(3).ResolveAsync(
            Arg.Any<Isbn13>(),
            Arg.Any<CancellationToken>(),
            false,
            IsbndbUsage.Scan,
            Arg.Any<IsbndbLookup?>());
    }

    private static IIsbndbLookupService CreateLookupService(Isbn13 isbn, IsbndbLookup result)
    {
        var lookups = Substitute.For<IIsbndbLookupService>();
        lookups.LookupManyAsync(Arg.Any<IReadOnlyList<Isbn13>>(), IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Isbn13, IsbndbLookup> { [isbn] = result });
        return lookups;
    }

    private static BookMetadataResult CreateIsbndbMetadata(Isbn13 isbn, IsbndbBook notice) =>
        new(
            isbn.Value,
            notice.Title,
            "Victor Hugo",
            notice.Publisher,
            1995,
            new Uri(notice.Image!),
            "ISBNdb",
            null,
            new DateTimeOffset(WorkerNow),
            CoverSource: "ISBNdb",
            IsbndbOutcome: IsbndbOutcome.Found,
            IsbndbNotice: notice,
            Pages: notice.Pages,
            Edition: notice.Edition,
            WorkKey: "MISERABLES|HUGO");

    private static BookMetadataResult CreateFallbackMetadata(Isbn13 isbn) =>
        new(
            isbn.Value,
            "Livre de secours",
            "Auteur de secours",
            "Éditeur",
            2000,
            null,
            "BnF",
            "bnf:work",
            new DateTimeOffset(WorkerNow),
            IsbndbOutcome: IsbndbOutcome.Deferred);

    private static IDateTimeProvider CreateClock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WorkerNow);
        return clock;
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
