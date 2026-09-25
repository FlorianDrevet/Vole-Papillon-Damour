using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Application.tests.CheckoutPassages;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class GetMyRecommendationsQueryHandlerTests
{
    private const string RecentSeed = "9782070612758";
    private const string OlderSeed = "9782203001015";
    private const string SharedCandidate = "9782253006329";
    private const string SingleCandidate = "9782070363735";
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CurrentGeneration = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Handle_WhenPreferenceIsDisabled_ReturnsDisabledWithoutReadingGenerationOrNeighbors()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.SetRecommendationPreferenceAsync(member, enabled: false);
        fixture.ThrowWhenRecommendationDataIsRead = true;

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(RecommendationStatus.Disabled);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenMemberHasNoAssociatedPurchases_ReturnsNoPurchases()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(RecommendationStatus.NoPurchases);
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DoesNotUseVoidedPurchaseLinesAsSeeds()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var passage = await fixture.AddAssociatedPassageAsync(member, Now, RecentSeed);
        (await fixture.Context.CheckoutPassageLines.SingleAsync()).Void(Now.AddMinutes(1));
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(RecommendationStatus.NoPurchases);
        result.Value.Items.Should().BeEmpty();
        passage.Status.Should().Be(CheckoutPassageStatus.Associated);
    }

    [Fact]
    public async Task Handle_NeverRecommendsAnIsbnAlreadyPurchasedByMember()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-1), RecentSeed);
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-2), SharedCandidate);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.9f, NeighborReason.NextTome);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExcludesWorksBoughtBeforeTheTwentyMostRecentSeedEditions()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var purchaseIsbns = Enumerable.Range(0, 21).Select(CreateValidIsbn).ToArray();
        await fixture.AddCatalogBookAsync(
            purchaseIsbns[20],
            "Œuvre achetée auparavant",
            available: false,
            workId: "bnf-work-older-purchase");
        await fixture.AddCatalogBookAsync(
            SharedCandidate,
            "Autre édition de l’œuvre achetée",
            workId: "bnf-work-older-purchase");

        for (var index = 0; index < 20; index++)
        {
            await fixture.AddAssociatedPassageAsync(
                member,
                Now.AddMinutes(-index),
                purchaseIsbns[index]);
        }

        await fixture.AddAssociatedPassageAsync(member, Now.AddHours(-3), purchaseIsbns[20]);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(
            CurrentGeneration,
            purchaseIsbns[0],
            1,
            SharedCandidate,
            0.9f,
            NeighborReason.Theme);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DoesNotRecommendAnIsbnFromMemberSelection()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddCatalogBookAsync(RecentSeed, "Graine récente", available: false);
        await fixture.AddCatalogBookAsync(SharedCandidate, "Dans ma sélection");
        await fixture.AddAssociatedPassageAsync(member, Now, RecentSeed);
        await fixture.AddSelectionAsync(member, SharedCandidate);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.9f, NeighborReason.Theme);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FiltersExhaustedHiddenAndRedirectedBooksButKeepsAnnouncedBooks()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddCatalogBookAsync(RecentSeed, "Livre acheté", available: false);
        await fixture.AddCatalogBookAsync(SharedCandidate, "Épuisé", available: false);
        await fixture.AddCatalogBookAsync(SingleCandidate, "Annoncé", available: false);
        var hiddenIsbn = CreateValidIsbn(70);
        var redirectedIsbn = CreateValidIsbn(71);
        var canonicalIsbn = CreateValidIsbn(72);
        var hidden = await fixture.AddCatalogBookAsync(hiddenIsbn, "Masqué", available: true);
        hidden.UpdateCatalogVisibility(true, Now);
        var canonical = await fixture.AddCatalogBookAsync(canonicalIsbn, "Cible canonique", available: true);
        var redirected = await fixture.AddCatalogBookAsync(redirectedIsbn, "Fusionné", available: true);
        redirected.RedirectTo(canonical.Id);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddAssociatedPassageAsync(member, Now, RecentSeed);
        await fixture.AddRecommendationAnnouncementAsync(SingleCandidate, quantity: 2);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.9f, NeighborReason.Theme);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 2, SingleCandidate, 0.8f, NeighborReason.Theme);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 3, hiddenIsbn, 0.7f, NeighborReason.Theme);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 4, redirectedIsbn, 0.6f, NeighborReason.Theme);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Book.Isbn13.Should().Be(SingleCandidate);
        result.Value.Items[0].Book.QuantityAnnounced.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SumsContributionsAcrossSeedsBeforeRankingCandidates()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddCatalogBookAsync(RecentSeed, "Achat récent", available: false);
        await fixture.AddCatalogBookAsync(OlderSeed, "Achat précédent", available: false);
        await fixture.AddCatalogBookAsync(SharedCandidate, "Voisin partagé");
        await fixture.AddCatalogBookAsync(SingleCandidate, "Voisin simple");
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-1), RecentSeed);
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-2), OlderSeed);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.7f, NeighborReason.Theme);
        fixture.AddRecommendationNeighbor(CurrentGeneration, OlderSeed, 1, SharedCandidate, 0.7f, NeighborReason.SameAuthor);
        fixture.AddRecommendationNeighbor(CurrentGeneration, OlderSeed, 2, SingleCandidate, 0.7f, NeighborReason.Theme);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Select(item => item.Book.Isbn13)
            .Should().ContainInOrder(SharedCandidate, SingleCandidate);
    }

    [Fact]
    public async Task Handle_UsesTheStrongestSeedContributionForTitleAndReason()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddCatalogBookAsync(RecentSeed, "Titre historique récent", available: false);
        await fixture.AddCatalogBookAsync(OlderSeed, "Titre historique ancien", available: false);
        await fixture.AddCatalogBookAsync(SharedCandidate, "La suite", available: true);
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-1), RecentSeed);
        var recentBook = fixture.Context.Books.Local.Single(book => book.Id.Value == RecentSeed);
        recentBook.ApplyManualMetadata(
            new Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.BookMetadataPatch(
                "Titre actuel modifié", null, null, null, null, null, null, null,
                [Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.BookMetadataField.Title]),
            Now);
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-2), OlderSeed);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.8f, NeighborReason.NextTome);
        fixture.AddRecommendationNeighbor(CurrentGeneration, OlderSeed, 1, SharedCandidate, 0.9f, NeighborReason.SameAuthor);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var recommendation = result.Value.Items.Should().ContainSingle().Which;
        recommendation.SeedTitle.Should().Be("Titre historique récent");
        recommendation.Reason.Should().Be(NeighborReason.NextTome);
    }

    [Fact]
    public async Task Handle_RejectsCandidatesWhenEveryIndividualScoreIsBelowSimilarThreshold()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddCatalogBookAsync(RecentSeed, "Achat récent", available: false);
        await fixture.AddCatalogBookAsync(OlderSeed, "Achat précédent", available: false);
        await fixture.AddCatalogBookAsync(SharedCandidate, "Voisin sous le seuil");
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-1), RecentSeed);
        await fixture.AddAssociatedPassageAsync(member, Now.AddMinutes(-2), OlderSeed);
        await fixture.SetCurrentRecommendationGenerationAsync(CurrentGeneration);
        fixture.AddRecommendationNeighbor(CurrentGeneration, RecentSeed, 1, SharedCandidate, 0.4f, NeighborReason.Theme);
        fixture.AddRecommendationNeighbor(CurrentGeneration, OlderSeed, 1, SharedCandidate, 0.4f, NeighborReason.Theme);
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture, similarMinScore: 0.5f)
            .Handle(QueryFor(member), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    private static GetMyRecommendationsQueryHandler CreateHandler(
        CheckoutPassageFixture fixture,
        bool enabled = true,
        float similarMinScore = 0.5f,
        int personalMaxCount = 8)
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(enabled);
        settings.SimilarMinScore.Returns(similarMinScore);
        settings.PersonalMaxCount.Returns(personalMaxCount);
        return new GetMyRecommendationsQueryHandler(
            fixture.Context,
            new MemberIdentityService(fixture.Context, new CheckoutPassageTestClock(Now)),
            settings,
            new CheckoutPassageTestClock(Now));
    }

    private static GetMyRecommendationsQuery QueryFor(User member, int limit = 4) => new(
        member.Id.Value.ToString("D"),
        member.Email,
        member.Name?.FirstName,
        member.Name?.LastName,
        limit);

    private static string CreateValidIsbn(int index)
    {
        var body = $"97800000{index:D4}";
        var sum = body.Select((digit, position) =>
            (digit - '0') * (position % 2 == 0 ? 1 : 3)).Sum();
        return body + ((10 - (sum % 10)) % 10);
    }
}
