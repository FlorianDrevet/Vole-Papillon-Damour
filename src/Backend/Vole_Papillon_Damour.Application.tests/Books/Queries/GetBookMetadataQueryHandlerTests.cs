using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.GetBookMetadata;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries;

public sealed class GetBookMetadataQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenResolverFindsMetadata_ReturnsTheMetadata()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var metadata = new BookMetadataResult(
            isbn13.Value,
            "Le Petit Prince",
            "Antoine de Saint-Exupéry",
            "Gallimard",
            1946,
            new Uri("https://covers.example.test/petit-prince.jpg"),
            "BnF",
            "OL123W",
            new DateTimeOffset(2026, 9, 3, 8, 0, 0, TimeSpan.Zero));
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns(metadata);
        var handler = new GetBookMetadataQueryHandler(resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(metadata);
    }

    [Fact]
    public async Task Handle_WhenResolverFindsNothing_ReturnsNotFoundError()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var resolver = Substitute.For<IBibliographicMetadataResolver>();
        resolver.ResolveAsync(isbn13, Arg.Any<CancellationToken>()).Returns((BookMetadataResult?)null);
        var handler = new GetBookMetadataQueryHandler(resolver);

        var result = await handler.Handle(new GetBookMetadataQuery(isbn13), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.MetadataNotFound");
    }
}
