using FluentAssertions;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Domain.tests.RecommendationAggregateTests;

public sealed class BookSimilarityProfileTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 3, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void NewProfile_NeedsNoEmbeddingUntilItHasText()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");

        profile.NeedsEmbedding.Should().BeFalse();
        profile.SetProfileText("Les hommes qui n'aimaient pas les femmes. Auteur : Stieg Larsson", "hash-1");
        profile.NeedsEmbedding.Should().BeTrue();
    }

    [Fact]
    public void RecordEmbedding_ForCurrentText_ClearsNeedsEmbedding()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");

        profile.RecordEmbedding(new byte[2048], "hash-1", Now);

        profile.NeedsEmbedding.Should().BeFalse();
    }

    [Fact]
    public void ChangingText_AfterEmbedding_NeedsEmbeddingAgain()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");
        profile.RecordEmbedding(new byte[2048], "hash-1", Now);

        profile.SetProfileText("texte modifié", "hash-2");

        profile.NeedsEmbedding.Should().BeTrue();
    }

    [Fact]
    public void RecordEmbedding_WithWrongSize_Throws()
    {
        var profile = BookSimilarityProfile.Create("9782742793099");
        profile.SetProfileText("texte", "hash-1");

        var act = () => profile.RecordEmbedding(new byte[100], "hash-1", Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidIsbn_Throws()
    {
        var act = () => BookSimilarityProfile.Create("123");

        act.Should().Throw<ArgumentException>();
    }
}
