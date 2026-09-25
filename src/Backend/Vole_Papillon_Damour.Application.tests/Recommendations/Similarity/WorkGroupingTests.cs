using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using static Vole_Papillon_Damour.Application.tests.Recommendations.Similarity.SimilarityFeaturesTests;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class WorkGroupingTests
{
    private const string Long = "Un aviateur tombé en panne dans le désert rencontre un petit garçon venu d'une autre planète, qui lui raconte son voyage.";

    [Fact]
    public void Group_SharesTheLongestCleanSummaryAcrossEditionsOfAWork()
    {
        var recent = Edition("9782070612758", "Le petit prince", bnfWorkId: "W1", summary: Long);
        var old = Edition("9782070331000", "Le petit prince", bnfWorkId: "W1");

        var grouped = WorkGrouping.Group([recent, old]);

        grouped.Single(g => g.Edition.Isbn13 == "9782070331000").WorkSummary.Should().Be(Long);
    }

    [Fact]
    public void Group_FallsBackToOpenLibraryDescription()
    {
        var edition = Edition("9782253171676", "Les rivières pourpres", olDescription: "Deux enquêtes parallèles mènent deux policiers vers une université isolée des Alpes françaises.");

        WorkGrouping.Group([edition]).Single().WorkSummary.Should().StartWith("Deux enquêtes");
    }

    [Fact]
    public void Group_UsesTheMajorityAudienceOfTheWork()
    {
        var youth = Edition("9782070612758", "Le petit prince", bnfWorkId: "W1", collection: "Folio junior");
        var adult1 = Edition("9782075224055", "Le petit prince", bnfWorkId: "W1", collection: "Folio");
        var adult2 = Edition("9782070408504", "Le petit prince", bnfWorkId: "W1", collection: "Folio");

        WorkGrouping.Group([youth, adult1, adult2])
            .Should().OnlyContain(g => g.Features.Audience == SimilarityAudience.Adult);
    }
}
