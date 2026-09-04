using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

namespace Vole_Papillon_Damour.Infrastructure.tests.Bibliographic;

public sealed class BibliographicMetadataResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenBnfFindsMetadata_DoesNotCallOpenLibrary()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var bnfMetadata = CreateMetadata("BnF");
        var bnf = Substitute.For<IBnfSruClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.FindAsync(isbn13, Arg.Any<CancellationToken>()).Returns(bnfMetadata);
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            NullLogger<BibliographicMetadataResolver>.Instance);

        var result = await resolver.ResolveAsync(isbn13, CancellationToken.None);

        result.Should().Be(bnfMetadata);
        await openLibrary.DidNotReceiveWithAnyArgs().FindAsync(default, default);
    }

    [Fact]
    public async Task ResolveAsync_WhenBnfDoesNotFindMetadata_UsesOpenLibrary()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var openLibraryMetadata = CreateMetadata("OpenLibrary");
        var bnf = Substitute.For<IBnfSruClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.FindAsync(isbn13, Arg.Any<CancellationToken>()).Returns((BookMetadataResult?)null);
        openLibrary.FindAsync(isbn13, Arg.Any<CancellationToken>()).Returns(openLibraryMetadata);
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            NullLogger<BibliographicMetadataResolver>.Instance);

        var result = await resolver.ResolveAsync(isbn13, CancellationToken.None);

        result.Should().Be(openLibraryMetadata);
    }

    [Fact]
    public async Task ResolveAsync_WhenBnfIsUnavailable_StillUsesOpenLibrary()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var openLibraryMetadata = CreateMetadata("OpenLibrary");
        var bnf = Substitute.For<IBnfSruClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.FindAsync(isbn13, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BookMetadataResult?>(new HttpRequestException("bnf down")));
        openLibrary.FindAsync(isbn13, Arg.Any<CancellationToken>()).Returns(openLibraryMetadata);
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            NullLogger<BibliographicMetadataResolver>.Instance);

        var result = await resolver.ResolveAsync(isbn13, CancellationToken.None);

        result.Should().Be(openLibraryMetadata);
    }

    [Fact]
    public async Task ResolveAsync_WhenAllProvidersAreUnavailable_ThrowsInsteadOfReturningNotFound()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var bnf = Substitute.For<IBnfSruClient>();
        var openLibrary = Substitute.For<IOpenLibraryClient>();
        bnf.FindAsync(isbn13, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BookMetadataResult?>(new HttpRequestException("bnf down")));
        openLibrary.FindAsync(isbn13, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<BookMetadataResult?>(new HttpRequestException("open library down")));
        var resolver = new BibliographicMetadataResolver(
            bnf,
            openLibrary,
            NullLogger<BibliographicMetadataResolver>.Instance);

        var action = () => resolver.ResolveAsync(isbn13, CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    private static BookMetadataResult CreateMetadata(string source) => new(
        "9782070363735",
        "Le Petit Prince",
        "Antoine de Saint-Exupéry",
        "Gallimard",
        1946,
        new Uri("https://covers.example.test/book.jpg"),
        source,
        "OL123W",
        new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero));
}
