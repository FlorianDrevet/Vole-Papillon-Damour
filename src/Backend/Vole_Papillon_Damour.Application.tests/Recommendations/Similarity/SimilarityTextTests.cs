using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class SimilarityTextTests
{
    [Theory]
    [InlineData("L'Élégance du hérisson", "l elegance du herisson")]
    [InlineData("Œuvres  complètes", "oeuvres completes")]
    [InlineData(null, "")]
    public void Normalize_LowersStripsAccentsAndPunctuation(string? input, string expected) =>
        SimilarityText.Normalize(input).Should().Be(expected);

    [Fact]
    public void CleanSummary_RejectsShortTitleCopyExtractAndEditionText()
    {
        SimilarityText.CleanSummary("Le Comte de Monte-Cristo", "Le comte de Monte-Cristo").Should().BeNull();
        SimilarityText.CleanSummary("« — Monsieur le directeur, c'est justement parce que je suis un homme tranquille, auquel on n'a rien à reprocher »", "Germinal").Should().BeNull();
        SimilarityText.CleanSummary("Une édition abrégée qui transforme un roman du XIXe siècle. Les points forts de cette édition : des outils simples à chaque étape.", "Les misérables").Should().BeNull();
    }

    [Fact]
    public void CleanSummary_KeepsARealSummaryAndCapsItsLength()
    {
        var summary = "Jean Valjean sauve Marius de la barricade et l'emmène chez son grand-père avec l'aide de Javert. " + new string('x', 2000);

        var cleaned = SimilarityText.CleanSummary(summary, "Les misérables");

        cleaned.Should().StartWith("Jean Valjean sauve Marius");
        cleaned!.Length.Should().Be(1500);
    }
}
