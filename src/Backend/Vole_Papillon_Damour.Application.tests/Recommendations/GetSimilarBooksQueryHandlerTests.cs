using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetSimilarBooks;
using Vole_Papillon_Damour.Application.tests.Books.Queries;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class GetSimilarBooksQueryHandlerTests
{
    private const string SourceIsbn = "9782070408504";
    private const string FirstNeighborIsbn = "9782070363735";
    private const string SecondNeighborIsbn = "9782253006329";
    private static readonly DateTime Now = PublicCatalogQueryHandlerTests.Now;
    private static readonly Guid CurrentGeneration = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_PreservesRankOrderAndNeighborReason()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Tome suivant", "Auteur", quantityAvailable: 1);
        fixture.AddBook(SecondNeighborIsbn, "Même auteur", "Auteur", quantityAvailable: 1);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 3);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 1, FirstNeighborIsbn, 0.86f, NeighborReason.NextTome);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 2, SecondNeighborIsbn, 0.82f, NeighborReason.SameAuthor);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture).Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Select(item => item.Book.Isbn13).Should().Equal(FirstNeighborIsbn, SecondNeighborIsbn);
        result.Value.Select(item => item.Reason).Should().Equal(NeighborReason.NextTome, NeighborReason.SameAuthor);
    }

    [Fact]
    public async Task Handle_RejectsExhaustedBooksButKeepsAnnouncedBooks()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Épuisé", "Auteur");
        var announced = fixture.AddBook(SecondNeighborIsbn, "Annoncé", "Auteur");
        fixture.AddAnnouncement(announced, quantity: 2);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 3);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 1, FirstNeighborIsbn, 0.9f, NeighborReason.Theme);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 2, SecondNeighborIsbn, 0.8f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture, minimumCount: 1)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().ContainSingle().Which.Book.Isbn13.Should().Be(SecondNeighborIsbn);
        result.Value[0].Book.QuantityAnnounced.Should().Be(2);
    }

    [Fact]
    public async Task Handle_RejectsHiddenAndRedirectedBooks()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Masqué", "Auteur", hidden: true);
        var redirectedTarget = fixture.AddBook("9782266233200", "Cible canonique", "Auteur");
        var redirected = fixture.AddBook(SecondNeighborIsbn, "Redirigé", "Auteur");
        redirected.RedirectTo(redirectedTarget.Id);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 4);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 1, FirstNeighborIsbn, 0.9f, NeighborReason.Theme);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 2, SecondNeighborIsbn, 0.8f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture, minimumCount: 1)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RejectsNeighborsBelowConfiguredThreshold()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Livre voisin", "Auteur", quantityAvailable: 1);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 2);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 1, FirstNeighborIsbn, 0.49f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture, minimumCount: 1, minimumScore: 0.5f)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyWhenFewerThanMinimumNeighborsAreDisplayable()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Livre voisin", "Auteur", quantityAvailable: 1);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 2);
        fixture.AddNeighbor(CurrentGeneration, SourceIsbn, 1, FirstNeighborIsbn, 0.9f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture, minimumCount: 2)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsEmptyWithoutCurrentGeneration()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Livre voisin", "Auteur", quantityAvailable: 1);
        fixture.AddNeighbor(Guid.NewGuid(), SourceIsbn, 1, FirstNeighborIsbn, 0.9f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OnlyReadsNeighborsFromCurrentGeneration()
    {
        await using var fixture = await PublicCatalogFixture.CreateAsync();
        fixture.AddBook(SourceIsbn, "Livre source", "Auteur");
        fixture.AddBook(FirstNeighborIsbn, "Livre ancien", "Auteur", quantityAvailable: 1);
        await fixture.SetCurrentGenerationAsync(CurrentGeneration, 2);
        fixture.AddNeighbor(Guid.NewGuid(), SourceIsbn, 1, FirstNeighborIsbn, 0.9f, NeighborReason.Theme);
        await fixture.SaveAsync();

        var result = await CreateHandler(fixture)
            .Handle(new GetSimilarBooksQuery(SourceIsbn), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    private static GetSimilarBooksQueryHandler CreateHandler(
        PublicCatalogFixture fixture,
        int minimumCount = 2,
        float minimumScore = 0.5f)
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        settings.SimilarMinScore.Returns(minimumScore);
        settings.SimilarMaxCount.Returns(5);
        settings.SimilarMinCount.Returns(minimumCount);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new GetSimilarBooksQueryHandler(fixture.Context, settings, clock);
    }
}
