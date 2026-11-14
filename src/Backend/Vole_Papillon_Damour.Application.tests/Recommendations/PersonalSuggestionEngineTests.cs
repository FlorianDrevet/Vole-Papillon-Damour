using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Common;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class PersonalSuggestionEngineTests
{
    private static readonly DateTimeOffset FirstSeen =
        new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Suggest_CalculatesHeartsFromBestScoreAndAddsOneForMultipleContributors()
    {
        var engine = CreateEngine();
        var seeds = new[]
        {
            new PersonalSuggestionSeed("9782070612758", "Premier favori"),
            new PersonalSuggestionSeed("9782203001015", "Second favori"),
        };
        var candidates = new[]
        {
            Book("9782253006329", "Cinq cœurs"),
            Book("9782070363735", "Deux cœurs"),
            Book("9782266233200", "Deux contributeurs"),
        };
        var neighbors = new[]
        {
            Neighbor(seeds[0], candidates[0], 0.83f),
            Neighbor(seeds[0], candidates[1], 0.60f),
            Neighbor(seeds[0], candidates[2], 0.60f),
            Neighbor(seeds[1], candidates[2], 0.60f),
        };

        var result = engine.Suggest(
            seeds,
            neighbors,
            EmptyExclusions(),
            candidates,
            minimumHearts: 1,
            limit: 10);

        result.Select(item => (item.Book.Isbn13, item.Hearts))
            .Should()
            .ContainInOrder(
                ("9782253006329", 5),
                ("9782266233200", 3),
                ("9782070363735", 2));
    }

    [Fact]
    public void Suggest_MinimumHeartsFourFiltersThreeHeartSuggestions()
    {
        var seed = new PersonalSuggestionSeed("9782070612758", "Favori");
        var candidates = new[]
        {
            Book("9782253006329", "Deux contributeurs"),
            Book("9782070363735", "Deux cœurs"),
        };
        var result = CreateEngine().Suggest(
            [seed, new PersonalSuggestionSeed("9782203001015", "Autre favori")],
            [
                Neighbor(seed, candidates[0], 0.60f),
                Neighbor(new PersonalSuggestionSeed("9782203001015", "Autre favori"), candidates[0], 0.60f),
                Neighbor(seed, candidates[1], 0.60f),
            ],
            EmptyExclusions(),
            candidates,
            minimumHearts: 4,
            limit: 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Suggest_ExcludesPurchasedFavoriteSelectionAndUnavailableWorksButKeepsAnnouncedBooks()
    {
        var seed = new PersonalSuggestionSeed("9782070612758", "Favori source");
        var candidates = new[]
        {
            Book("9782253006329", "Déjà acheté", workId: "bought-work"),
            Book("9782070363735", "Autre édition favorite", workId: "favorite-work"),
            Book("9782266233200", "Dans la sélection"),
            Book("9782203001015", "Épuisé", available: false),
            Book("9782742793099", "Annoncé", available: false, announced: true),
            Book("9782742765010", "Disponible", workId: "new-work"),
        };
        var exclusions = new PersonalSuggestionExclusions(
            PurchasedIsbns: new HashSet<string>(["9782253006329"], StringComparer.Ordinal),
            PurchasedWorkIds: new HashSet<string>(["bought-work"], StringComparer.Ordinal),
            FavoriteIsbns: new HashSet<string>(["9782070363735"], StringComparer.Ordinal),
            FavoriteWorkIds: new HashSet<string>(["favorite-work"], StringComparer.Ordinal),
            SelectionIsbns: new HashSet<string>(["9782266233200"], StringComparer.Ordinal));

        var result = CreateEngine().Suggest(
            [seed],
            candidates.Select(candidate => Neighbor(seed, candidate, 0.70f)),
            exclusions,
            candidates,
            minimumHearts: 1,
            limit: 10);

        result.Select(item => item.Book.Isbn13)
            .Should()
            .Equal("9782742765010", "9782742793099");
    }

    [Fact]
    public void Suggest_UsesBestContributorReasonAndReportsOtherContributors()
    {
        var firstSeed = new PersonalSuggestionSeed("9782070612758", "Premier favori");
        var secondSeed = new PersonalSuggestionSeed("9782203001015", "La suite");
        var candidate = Book("9782253006329", "Livre suivant");

        var result = CreateEngine().Suggest(
            [firstSeed, secondSeed],
            [
                new PersonalSuggestionNeighbor(firstSeed.Isbn13, candidate.Isbn13, 0.70f, NeighborReason.Theme),
                new PersonalSuggestionNeighbor(secondSeed.Isbn13, candidate.Isbn13, 0.80f, NeighborReason.NextTome),
            ],
            EmptyExclusions(),
            [candidate],
            minimumHearts: 1,
            limit: 10);

        var suggestion = result.Should().ContainSingle().Which;
        suggestion.Reason.Should().Be(NeighborReason.NextTome);
        suggestion.SeedTitle.Should().Be("La suite");
        suggestion.OtherContributorCount.Should().Be(1);
    }

    [Fact]
    public void Suggest_ArrivedAfterFilterExcludesBooksSeenOnOrBeforeTheCursor()
    {
        var seed = new PersonalSuggestionSeed("9782070612758", "Favori");
        var candidate = Book("9782253006329", "Avant la bourse");

        var result = CreateEngine().Suggest(
            [seed],
            [Neighbor(seed, candidate, 0.80f)],
            EmptyExclusions(),
            [candidate],
            minimumHearts: 1,
            limit: 10,
            arrivedAfter: FirstSeen);

        result.Should().BeEmpty();
    }

    private static PersonalSuggestionEngine CreateEngine()
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.SimilarMinScore.Returns(0.50f);
        settings.HeartThresholds.Returns([0.50f, 0.58f, 0.66f, 0.74f, 0.82f]);
        return new PersonalSuggestionEngine(settings);
    }

    private static PersonalSuggestionExclusions EmptyExclusions() => new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    private static PersonalSuggestionNeighbor Neighbor(
        PersonalSuggestionSeed seed,
        PublicCatalogBookResult candidate,
        float score) => new(seed.Isbn13, candidate.Isbn13, score, NeighborReason.Theme);

    private static PublicCatalogBookResult Book(
        string isbn13,
        string title,
        string? workId = null,
        bool available = true,
        bool announced = false) => new(
        isbn13,
        title,
        "Auteur",
        null,
        null,
        null,
        "fr",
        null,
        workId,
        null,
        available ? 1 : 0,
        announced ? 1 : 0,
        null,
        null,
        FirstSeen,
        FirstSeen,
        false);
}
