using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Queries.SearchBibliographicReferences;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.SearchBibliographicReferences;

public sealed class SearchBibliographicReferencesQueryHandlerTests
{
    private static readonly DateTime Now =
        new(2026, 9, 6, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenQueryIsValid_ReturnsTrimmedReferencesAndPagination()
    {
        var searchService = Substitute.For<IBibliographicSearchService>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var items = new[]
        {
            new BookReferenceSearchItem(
                "9782070363735",
                "OL123W",
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1946,
                new Uri("https://covers.openlibrary.org/b/id/1-M.jpg"),
                "OpenLibrary")
        };
        searchService.SearchAsync("Le Petit Prince", 2, 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<BookReferenceSearchItem>>(items));

        var handler = new SearchBibliographicReferencesQueryHandler(searchService, clock);

        var result = await handler.Handle(
            new SearchBibliographicReferencesQuery("  Le Petit Prince  ", 2, 5),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Query.Should().Be("Le Petit Prince");
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(5);
        result.Value.Items.Should().ContainSingle().Which.Isbn13.Should().Be("9782070363735");
        await searchService.Received(1).SearchAsync(
            "Le Petit Prince",
            2,
            5,
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    public async Task Handle_WhenQueryIsTooShort_ReturnsValidationWithoutCallingProvider(string? query)
    {
        var searchService = Substitute.For<IBibliographicSearchService>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new SearchBibliographicReferencesQueryHandler(searchService, clock);

        var result = await handler.Handle(
            new SearchBibliographicReferencesQuery(query!, 1, 20),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.InvalidReferenceSearch");
        await searchService.DidNotReceive().SearchAsync(
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenProviderIsUnavailable_ReturnsFailure()
    {
        var searchService = Substitute.For<IBibliographicSearchService>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        searchService.SearchAsync("isbn", 1, 20, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<BookReferenceSearchItem>>(
                new HttpRequestException("provider unavailable")));
        var handler = new SearchBibliographicReferencesQueryHandler(searchService, clock);

        var result = await handler.Handle(
            new SearchBibliographicReferencesQuery("isbn"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.ReferenceSearchUnavailable");
    }
}
