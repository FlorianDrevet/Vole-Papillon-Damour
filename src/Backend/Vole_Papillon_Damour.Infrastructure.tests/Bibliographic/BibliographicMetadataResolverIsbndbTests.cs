using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BibliographicMetadataResolverIsbndbTests
{
    [Fact]
    public async Task ResolveAsync_WhenIsbndbFindsBook_ReturnsCompleteNoticeWithoutCallingFreeProviders()
    {
        var isbn = ParseIsbn("9782070409228");
        var notice = new IsbndbBook
        {
            Isbn13 = isbn.Value,
            Title = "Les Misérables",
            Authors = ["Hugo, Victor"],
            Publisher = "Gallimard",
            DatePublished = "1995",
            Pages = 512,
            Edition = "Folio classique",
            Image = "https://images.isbndb.com/covers/book.jpg",
            Subjects = ["Romans", "Histoire"],
            Language = "fra"
        };
        var lookup = Substitute.For<IIsbndbLookupService>();
        lookup.LookupAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.Found(notice));
        var bnf = Substitute.For<IBnfSruClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        var googleBooks = Substitute.For<IGoogleBooksClient>();
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            googleBooks,
            NullLogger<BibliographicMetadataResolver>.Instance,
            lookup);

        var result = await resolver.ResolveAsync(isbn, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Source.Should().Be("ISBNdb");
        result.IsbndbOutcome.Should().Be(IsbndbOutcome.Found);
        result.IsbndbNotice.Should().Be(notice);
        result.Title.Should().Be("Les Misérables");
        result.Authors.Should().Be("Victor Hugo");
        result.Pages.Should().Be(512);
        result.Edition.Should().Be("Folio classique");
        result.WorkKey.Should().Be("MISERABLES|HUGO");
        await bnf.DidNotReceiveWithAnyArgs().FindAsync(default, default);
        await openLibrary.DidNotReceiveWithAnyArgs().FindAsync(default, default);
        await googleBooks.DidNotReceiveWithAnyArgs().FindAsync(default, default);
    }

    [Fact]
    public async Task ResolveAsync_WhenIsbndbReportsNotFound_UsesBnFFallbackAndMarksOutcome()
    {
        var isbn = ParseIsbn("9782070409228");
        var fallback = new BookMetadataResult(
            isbn.Value,
            "Les Misérables",
            "Victor Hugo",
            "Gallimard",
            1995,
            null,
            "BnF",
            "bnf:work",
            DateTimeOffset.UtcNow);
        var lookup = Substitute.For<IIsbndbLookupService>();
        lookup.LookupAsync(isbn, IsbndbUsage.Scan, Arg.Any<CancellationToken>())
            .Returns(new IsbndbLookup.NotFound());
        var bnf = Substitute.For<IBnfSruClient>();
        bnf.FindAsync(isbn, Arg.Any<CancellationToken>()).Returns(fallback);
        var resolver = new BibliographicMetadataResolver(
            bnf,
            Substitute.For<IOpenLibraryClient>(),
            Substitute.For<IGoogleBooksClient>(),
            NullLogger<BibliographicMetadataResolver>.Instance,
            lookup);

        var result = await resolver.ResolveAsync(isbn, CancellationToken.None);

        result.Should().Be(fallback with { IsbndbOutcome = IsbndbOutcome.NotFound });
        await bnf.Received(1).FindAsync(isbn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_UsesPrefetchedBatchOutcomeWithoutMakingAnotherIsbndbCall()
    {
        var isbn = ParseIsbn("9782070409228");
        var lookup = Substitute.For<IIsbndbLookupService>();
        var bnf = Substitute.For<IBnfSruClient>();
        var resolver = new BibliographicMetadataResolver(
            bnf,
            Substitute.For<IOpenLibraryClient>(),
            Substitute.For<IGoogleBooksClient>(),
            NullLogger<BibliographicMetadataResolver>.Instance,
            lookup);
        var prefetched = new IsbndbLookup.QuotaExhausted(DateTimeOffset.UtcNow.AddHours(1).UtcDateTime);
        bnf.FindAsync(isbn, Arg.Any<CancellationToken>()).Returns(new BookMetadataResult(
            isbn.Value,
            "Livre de secours",
            "Auteur",
            null,
            null,
            null,
            "BnF",
            "bnf:work",
            DateTimeOffset.UtcNow));

        var result = await resolver.ResolveAsync(
            isbn,
            CancellationToken.None,
            usage: IsbndbUsage.CatchUp,
            prefetchedIsbndbLookup: prefetched);

        result!.IsbndbOutcome.Should().Be(IsbndbOutcome.Deferred);
        await lookup.DidNotReceiveWithAnyArgs().LookupAsync(default, default, default);
    }

    [Fact]
    public async Task ResolveWorkIdAsync_UsesBnfFirstAndDoesNotCallOpenLibraryWhenFound()
    {
        var isbn = ParseIsbn("9782070409228");
        var bnf = Substitute.For<IBnfSruClient>();
        bnf.FindAsync(isbn, Arg.Any<CancellationToken>()).Returns(new BookMetadataResult(
            isbn.Value,
            null,
            null,
            null,
            null,
            null,
            "BnF",
            "bnf:work",
            DateTimeOffset.UtcNow));
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            Substitute.For<IGoogleBooksClient>(),
            NullLogger<BibliographicMetadataResolver>.Instance);

        var workId = await resolver.ResolveWorkIdAsync(isbn, CancellationToken.None);

        workId.Should().Be("bnf:work");
        await bnf.Received(1).FindAsync(isbn, Arg.Any<CancellationToken>());
        await openLibrary.DidNotReceiveWithAnyArgs().FindAsync(default, default);
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
