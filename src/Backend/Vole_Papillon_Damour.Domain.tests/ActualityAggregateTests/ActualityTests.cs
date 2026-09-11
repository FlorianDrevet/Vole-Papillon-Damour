using FluentAssertions;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.ActualityAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.ActualityAggregateTests;

public sealed class ActualityTests
{
    private static readonly Uri PrincipalImage = new("https://cdn.example.test/principal.jpg");
    private static readonly DateTimeOffset PublishedAt =
        new(2026, 9, 10, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ManualActuality_IsPublishedAndDoesNotNeedTitleReview()
    {
        var actuality = Actuality.Create(
            "Une actualité",
            "Un article",
            PrincipalImage,
            null,
            null,
            [],
            PublishedAt);

        actuality.Status.Should().Be(ActualityStatus.Published);
        actuality.TitleNeedsReview.Should().BeFalse();
        actuality.ImportedAt.Should().BeNull();
    }

    [Fact]
    public void CreateImported_CreatesADraftWithItsImportMetadata()
    {
        var importedAt = PublishedAt.AddMinutes(5);
        var actuality = Actuality.CreateImported(
            "Actualité du 10 septembre 2026",
            "Un article importé",
            PrincipalImage,
            new Uri("https://www.instagram.com/p/example/"),
            [],
            PublishedAt,
            importedAt,
            titleNeedsReview: true);

        actuality.Status.Should().Be(ActualityStatus.Draft);
        actuality.TitleNeedsReview.Should().BeTrue();
        actuality.ImportedAt.Should().Be(importedAt);
        actuality.InstagramLink.Should().Be("https://www.instagram.com/p/example/");
    }

    [Fact]
    public void Publish_WhenTitleOrArticleIsEmpty_RefusesToPublish()
    {
        var actuality = Actuality.CreateImported(
            "Titre valide",
            string.Empty,
            PrincipalImage,
            null,
            [],
            PublishedAt,
            PublishedAt,
            titleNeedsReview: true);

        var published = actuality.Publish();

        published.Should().BeFalse();
        actuality.Status.Should().Be(ActualityStatus.Draft);
    }

    [Fact]
    public void Update_OnADraftMarksTheTitleAsReviewedWithoutPublishingIt()
    {
        var actuality = Actuality.CreateImported(
            "Titre proposé",
            "Article",
            PrincipalImage,
            null,
            [],
            PublishedAt,
            PublishedAt,
            titleNeedsReview: true);

        actuality.Update(
            "Titre corrigé",
            "Article corrigé",
            PrincipalImage,
            null,
            null,
            [],
            PublishedAt);

        actuality.Status.Should().Be(ActualityStatus.Draft);
        actuality.TitleNeedsReview.Should().BeFalse();
    }
}
