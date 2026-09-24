using System.Numerics.Tensors;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public sealed record NeighborCandidate(string NeighborIsbn13, float Score, NeighborReason Reason);

public static class NeighborComputer
{
    public const float SameSeriesBonus = 0.20f;
    public const float NextTomeBonus = 0.10f;
    public const float SameAuthorBonus = 0.08f;
    public const float SameFormBonus = 0.03f;
    public const float OppositeAudiencePenalty = 0.10f;

    /// <summary>Vecteurs normalisés (norme 1) : le produit scalaire est le cosinus.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>> Compute(
        IReadOnlyList<SimilarityFeatures> features,
        IReadOnlyList<float[]> vectors,
        int neighborsPerBook)
    {
        if (features.Count != vectors.Count)
        {
            throw new ArgumentException("One vector per book is required.", nameof(vectors));
        }

        var results = new IReadOnlyList<NeighborCandidate>[features.Count];
        Parallel.For(0, features.Count, i =>
        {
            var heap = new PriorityQueue<NeighborCandidate, float>(neighborsPerBook + 1);
            for (var j = 0; j < features.Count; j++)
            {
                if (i == j || SimilarityFeatureBuilder.SameWork(features[i], features[j]))
                {
                    continue;
                }

                var (bonus, reason) = Adjust(features[i], features[j]);
                var score = TensorPrimitives.Dot<float>(vectors[i], vectors[j]) + bonus;
                heap.Enqueue(new NeighborCandidate(features[j].Isbn13, score, reason), score);
                if (heap.Count > neighborsPerBook)
                {
                    heap.Dequeue(); // retire le plus faible
                }
            }

            var ordered = new List<NeighborCandidate>(heap.Count);
            while (heap.Count > 0)
            {
                ordered.Add(heap.Dequeue());
            }

            ordered.Reverse();
            results[i] = ordered;
        });

        return features.Select((f, i) => (f.Isbn13, results[i]))
            .ToDictionary(x => x.Isbn13, x => x.Item2, StringComparer.Ordinal);
    }

    public static (float Bonus, NeighborReason Reason) Adjust(SimilarityFeatures query, SimilarityFeatures candidate)
    {
        var bonus = 0f;
        NeighborReason? reason = null;
        if (query.SeriesKey is not null && query.SeriesKey == candidate.SeriesKey)
        {
            bonus += SameSeriesBonus;
            reason = NeighborReason.SameSeries;
            if (query.Tome is { } tome && candidate.Tome == tome + 1)
            {
                bonus += NextTomeBonus;
                reason = NeighborReason.NextTome;
            }
        }

        if (query.Surnames.Overlaps(candidate.Surnames))
        {
            bonus += SameAuthorBonus;
            reason ??= NeighborReason.SameAuthor;
        }

        if (query.Form == candidate.Form)
        {
            bonus += SameFormBonus;
        }

        if ((query.Audience, candidate.Audience) is (SimilarityAudience.Youth, SimilarityAudience.Adult)
            or (SimilarityAudience.Adult, SimilarityAudience.Youth))
        {
            bonus -= OppositeAudiencePenalty;
        }

        return (bonus, reason ?? NeighborReason.Theme);
    }
}
