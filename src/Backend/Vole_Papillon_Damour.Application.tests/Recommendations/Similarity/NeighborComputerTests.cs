using FluentAssertions;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;
using static Vole_Papillon_Damour.Application.tests.Recommendations.Similarity.SimilarityFeaturesTests;

namespace Vole_Papillon_Damour.Application.tests.Recommendations.Similarity;

public sealed class NeighborComputerTests
{
    private static float[] Vector(params float[] head)
    {
        var vector = new float[4];
        head.CopyTo(vector, 0);
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    [Fact]
    public void Compute_ExcludesSelfAndOtherEditionsOfTheSameWork()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782266233200", "Dune", surnames: ["Herbert"], bnfWorkId: "W")),
            SimilarityFeatureBuilder.Build(Edition("9782221249420", "Dune", surnames: ["Herbert"], bnfWorkId: "W")),
            SimilarityFeatureBuilder.Build(Edition("9782266282000", "Hypérion", surnames: ["Simmons"])),
        };
        var vectors = new[] { Vector(1, 0), Vector(1, 0), Vector(0.9f, 0.1f) };

        var result = NeighborComputer.Compute(features, vectors, 30);

        result["9782266233200"].Select(n => n.NeighborIsbn13).Should().Equal("9782266282000");
    }

    [Fact]
    public void Compute_PutsTheNextTomeFirstWithReasonNextTome()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782742793099", "Les hommes qui n'aimaient pas les femmes", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 1)),
            SimilarityFeatureBuilder.Build(Edition("9782742765010", "La fille qui rêvait d'un bidon d'essence et d'une allumette", surnames: ["Larsson"], seriesTitle: "Millénium", seriesNumber: 2)),
            SimilarityFeatureBuilder.Build(Edition("9782253157533", "Miséricorde", surnames: ["Adler-Olsen"])),
        };
        var vectors = new[] { Vector(1, 0), Vector(0.5f, 0.5f), Vector(0.95f, 0.05f) };

        var first = NeighborComputer.Compute(features, vectors, 30)["9782742793099"][0];

        first.NeighborIsbn13.Should().Be("9782742765010");
        first.Reason.Should().Be(NeighborReason.NextTome);
    }

    [Fact]
    public void OppositeAudience_IsPenalizedNotExcluded()
    {
        var features = new[]
        {
            SimilarityFeatureBuilder.Build(Edition("9782226257017", "Percy Jackson", surnames: ["Riordan"], collection: "Wiz", publisher: "Albin Michel")),
            SimilarityFeatureBuilder.Build(Edition("9782070584628", "Harry Potter", surnames: ["Rowling"], collection: "Folio", publisher: "Gallimard")),
        };
        var vectors = new[] { Vector(1, 0), Vector(1, 0) };

        var neighbor = NeighborComputer.Compute(features, vectors, 30)["9782226257017"].Single();

        neighbor.NeighborIsbn13.Should().Be("9782070584628");
        neighbor.Score.Should().BeApproximately(1f + 0.03f - 0.10f, 0.0001f);
    }

    [Fact]
    public void Compute_KeepsAtMostTheRequestedNumberOfNeighbors()
    {
        var features = Enumerable.Range(0, 6)
            .Select(i => SimilarityFeatureBuilder.Build(Edition($"97820706127{i:D2}", $"Livre {i}", surnames: [$"Auteur{i}"])))
            .ToArray();
        var vectors = features.Select((_, i) => Vector(1, i)).ToArray();

        NeighborComputer.Compute(features, vectors, 3).Values.Should().OnlyContain(list => list.Count == 3);
    }

    [Fact]
    public void Reason_SameAuthorWhenNoSeries()
    {
        var q = SimilarityFeatureBuilder.Build(Edition("9782070360024", "L'étranger", surnames: ["Camus"]));
        var c = SimilarityFeatureBuilder.Build(Edition("9782070360420", "La peste", surnames: ["Camus"]));

        NeighborComputer.Adjust(q, c).Reason.Should().Be(NeighborReason.SameAuthor);
    }
}
