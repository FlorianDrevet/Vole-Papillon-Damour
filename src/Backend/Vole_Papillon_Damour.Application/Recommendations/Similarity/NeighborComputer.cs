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
        return ComputeForBooks(features, vectors, features.Select(feature => feature.Isbn13), neighborsPerBook);
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>> ComputeIncremental(
        IReadOnlyList<SimilarityFeatures> features,
        IReadOnlyList<float[]> vectors,
        IReadOnlyCollection<string> changedIsbns,
        IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>> currentNeighbors,
        int neighborsPerBook)
    {
        if (features.Count != vectors.Count)
        {
            throw new ArgumentException("One vector per book is required.", nameof(vectors));
        }

        ArgumentNullException.ThrowIfNull(changedIsbns);
        ArgumentNullException.ThrowIfNull(currentNeighbors);

        var changed = changedIsbns.ToHashSet(StringComparer.Ordinal);
        var featuresByIsbn = features.ToDictionary(feature => feature.Isbn13, StringComparer.Ordinal);
        var affected = new HashSet<string>(changed, StringComparer.Ordinal);
        foreach (var (isbn13, neighbors) in currentNeighbors)
        {
            if (neighbors.Any(neighbor => changed.Contains(neighbor.NeighborIsbn13)))
            {
                affected.Add(isbn13);
            }
        }

        var vectorByIsbn = features
            .Select((feature, index) => (feature.Isbn13, Vector: vectors[index]))
            .ToDictionary(item => item.Isbn13, item => item.Vector, StringComparer.Ordinal);
        foreach (var changedIsbn in changed)
        {
            if (!featuresByIsbn.TryGetValue(changedIsbn, out var candidate))
            {
                continue;
            }

            foreach (var query in features)
            {
                if (changed.Contains(query.Isbn13) || SimilarityFeatureBuilder.SameWork(query, candidate))
                {
                    continue;
                }

                currentNeighbors.TryGetValue(query.Isbn13, out var existing);
                existing ??= Array.Empty<NeighborCandidate>();
                if (existing.Any(neighbor => neighbor.NeighborIsbn13 == changedIsbn) ||
                    IsWithinTopNeighbors(
                        query,
                        vectorByIsbn[query.Isbn13],
                        candidate,
                        vectorByIsbn[changedIsbn],
                        existing,
                        neighborsPerBook))
                {
                    affected.Add(query.Isbn13);
                }
            }
        }

        var recomputed = ComputeForBooks(
            features,
            vectors,
            affected.Where(featuresByIsbn.ContainsKey),
            neighborsPerBook);
        var result = new Dictionary<string, IReadOnlyList<NeighborCandidate>>(StringComparer.Ordinal);
        foreach (var isbn13 in affected)
        {
            result[isbn13] = recomputed.TryGetValue(isbn13, out var neighbors)
                ? neighbors
                : Array.Empty<NeighborCandidate>();
        }

        return result;
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<NeighborCandidate>> ComputeForBooks(
        IReadOnlyList<SimilarityFeatures> features,
        IReadOnlyList<float[]> vectors,
        IEnumerable<string> queryIsbns,
        int neighborsPerBook)
    {
        if (features.Count != vectors.Count)
        {
            throw new ArgumentException("One vector per book is required.", nameof(vectors));
        }

        if (neighborsPerBook < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(neighborsPerBook));
        }

        var querySet = queryIsbns.ToHashSet(StringComparer.Ordinal);
        var results = new Dictionary<string, IReadOnlyList<NeighborCandidate>>(StringComparer.Ordinal);
        var indexByIsbn = features.Select((feature, index) => (feature.Isbn13, index))
            .ToDictionary(item => item.Isbn13, item => item.index, StringComparer.Ordinal);
        foreach (var queryIsbn in querySet)
        {
            if (!indexByIsbn.TryGetValue(queryIsbn, out var i))
            {
                results[queryIsbn] = Array.Empty<NeighborCandidate>();
                continue;
            }

            var heap = new PriorityQueue<NeighborCandidate, NeighborPriority>(
                Math.Max(1, neighborsPerBook + 1),
                NeighborPriorityComparer.Instance);
            for (var j = 0; j < features.Count; j++)
            {
                if (i == j || SimilarityFeatureBuilder.SameWork(features[i], features[j]))
                {
                    continue;
                }

                var (bonus, reason) = Adjust(features[i], features[j]);
                var score = TensorPrimitives.Dot<float>(vectors[i], vectors[j]) + bonus;
                var candidate = new NeighborCandidate(features[j].Isbn13, score, reason);
                heap.Enqueue(candidate, new NeighborPriority(score, candidate.NeighborIsbn13));
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

            ordered.Sort(BestFirstComparer.Instance);
            results[queryIsbn] = ordered;
        }

        return results;
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

    private static bool IsWithinTopNeighbors(
        SimilarityFeatures query,
        float[] queryVector,
        SimilarityFeatures candidate,
        float[] candidateVector,
        IReadOnlyList<NeighborCandidate> current,
        int neighborsPerBook)
    {
        if (neighborsPerBook == 0 || current.Count < neighborsPerBook)
        {
            return neighborsPerBook > 0;
        }

        var candidateScore = TensorPrimitives.Dot<float>(queryVector, candidateVector) +
                             Adjust(query, candidate).Bonus;
        var worst = current[^1];
        return candidateScore > worst.Score ||
               (candidateScore == worst.Score &&
                string.CompareOrdinal(candidate.Isbn13, worst.NeighborIsbn13) < 0);
    }

    private readonly record struct NeighborPriority(float Score, string Isbn13);

    private sealed class NeighborPriorityComparer : IComparer<NeighborPriority>
    {
        public static NeighborPriorityComparer Instance { get; } = new();

        public int Compare(NeighborPriority left, NeighborPriority right)
        {
            var byScore = left.Score.CompareTo(right.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(right.Isbn13, left.Isbn13);
        }
    }

    private sealed class BestFirstComparer : IComparer<NeighborCandidate>
    {
        public static BestFirstComparer Instance { get; } = new();

        public int Compare(NeighborCandidate? left, NeighborCandidate? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return 1;
            if (right is null) return -1;
            var byScore = right.Score.CompareTo(left.Score);
            return byScore != 0 ? byScore : string.CompareOrdinal(left.NeighborIsbn13, right.NeighborIsbn13);
        }
    }
}
