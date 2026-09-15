using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BibliographicSearchServiceTests
{
    private static readonly Uri OpenLibraryCover = new("https://covers.openlibrary.org/b/id/1-L.jpg");

    [Fact]
    public async Task SearchAsync_WhenOnlyBnfKnowsTheIsbn_ReturnsTheBnfEdition()
    {
        var bnf = Substitute.For<IBnfSruSearchClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.SearchAsync("9783366339568", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Items(Bnf("9783366339568", "Titre BnF")));
        openLibrary.SearchAsync("9783366339568", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Items());

        var result = await CreateService(bnf, openLibrary)
            .SearchAsync("9783366339568", 1, 20, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Isbn13.Should().Be("9783366339568");
        result[0].Source.Should().Be("BnF");
    }

    [Fact]
    public async Task SearchAsync_PutsBnfEditionsFirstAndCompletesThemWithOpenLibrary()
    {
        var bnf = Substitute.For<IBnfSruSearchClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Items(Bnf("9782070363735", "Le Petit Prince (BnF)")));
        openLibrary.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Items(
                OpenLibrary("9780156012195", "OL1W", "The Little Prince"),
                OpenLibrary("9782070363735", "OL2W", "Le Petit Prince (Open Library)"),
                OpenLibrary(null, "OL3W", "Sans ISBN")));

        var result = await CreateService(bnf, openLibrary)
            .SearchAsync("Petit Prince", 1, 20, CancellationToken.None);

        result.Select(item => item.Isbn13).Should().Equal("9782070363735", "9780156012195", null);
        result[0].Title.Should().Be("Le Petit Prince (BnF)");
        result[0].Source.Should().Be("BnF");
        result[0].WorkId.Should().Be("OL2W");
        result[0].CoverUrl.Should().Be(OpenLibraryCover);
        result[1].Source.Should().Be("OpenLibrary");
        result[2].WorkId.Should().Be("OL3W");
    }

    [Fact]
    public async Task SearchAsync_WhenBnfFails_ReturnsOpenLibraryEditions()
    {
        var bnf = Substitute.For<IBnfSruSearchClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BookReferenceSearchItem>>(
                new TaskCanceledException("timeout")));
        openLibrary.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Items(OpenLibrary("9782070363735", "OL2W", "Le Petit Prince")));

        var result = await CreateService(bnf, openLibrary)
            .SearchAsync("Petit Prince", 1, 20, CancellationToken.None);

        result.Should().ContainSingle().Which.Source.Should().Be("OpenLibrary");
    }

    [Fact]
    public async Task SearchAsync_WhenEveryProviderFails_ThrowsUnavailable()
    {
        var bnf = Substitute.For<IBnfSruSearchClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BookReferenceSearchItem>>(
                new HttpRequestException("bnf down")));
        openLibrary.SearchAsync("Petit Prince", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BookReferenceSearchItem>>(
                new HttpRequestException("open library down")));

        var search = () => CreateService(bnf, openLibrary)
            .SearchAsync("Petit Prince", 1, 20, CancellationToken.None);

        await search.Should().ThrowAsync<HttpRequestException>();
    }

    private static BibliographicSearchService CreateService(
        IBnfSruSearchClient bnf,
        IOpenLibraryClient openLibrary)
    {
        return new BibliographicSearchService(
            bnf,
            openLibrary,
            NullLogger<BibliographicSearchService>.Instance);
    }

    private static Task<IReadOnlyList<BookReferenceSearchItem>> Items(params BookReferenceSearchItem[] items)
    {
        return Task.FromResult<IReadOnlyList<BookReferenceSearchItem>>(items);
    }

    private static BookReferenceSearchItem Bnf(string isbn13, string title)
    {
        return new BookReferenceSearchItem(isbn13, null, title, "Auteur BnF", "Éditeur BnF", 1946, null, "BnF");
    }

    private static BookReferenceSearchItem OpenLibrary(string? isbn13, string workId, string title)
    {
        return new BookReferenceSearchItem(isbn13, workId, title, "Auteur OL", "Éditeur OL", 1943, OpenLibraryCover, "OpenLibrary");
    }
}
