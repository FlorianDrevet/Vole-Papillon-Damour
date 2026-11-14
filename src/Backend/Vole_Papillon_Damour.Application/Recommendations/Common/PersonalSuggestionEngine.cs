using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Common;

public sealed record PersonalSuggestionSeed(string Isbn13, string? Title);

public sealed record PersonalSuggestionNeighbor(
    string SeedIsbn13,
    string NeighborIsbn13,
    float Score,
    NeighborReason Reason);

public sealed record PersonalSuggestionExclusions(
    IReadOnlySet<string> PurchasedIsbns,
    IReadOnlySet<string> PurchasedWorkIds,
    IReadOnlySet<string> FavoriteIsbns,
    IReadOnlySet<string> FavoriteWorkIds,
    IReadOnlySet<string> SelectionIsbns);

/// <summary>Applies the shared scoring, exclusion, heart and ordering rules to personal suggestions.</summary>
public sealed class PersonalSuggestionEngine(IRecommendationSettings settings)
{
    private static readonly IReadOnlyList<float> FallbackHeartThresholds = [0.50f, 0.58f, 0.66f, 0.74f, 0.82f];

    public IReadOnlyList<PersonalRecommendation> Suggest(
        IReadOnlyCollection<PersonalSuggestionSeed> seeds,
        IEnumerable<PersonalSuggestionNeighbor> neighbors,
        PersonalSuggestionExclusions exclusions,
        IEnumerable<PublicCatalogBookResult> publicBooks,
        int minimumHearts,
        int limit,
        DateTimeOffset? arrivedAfter = null)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        ArgumentNullException.ThrowIfNull(neighbors);
        ArgumentNullException.ThrowIfNull(exclusions);
        ArgumentNullException.ThrowIfNull(publicBooks);
        if (minimumHearts is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHearts));
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        if (limit == 0 || seeds.Count == 0)
        {
            return [];
        }

        var seedByIsbn = seeds
            .Where(seed => Isbn13.TryCreate(seed.Isbn13, out _))
            .GroupBy(seed => seed.Isbn13, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select((seed, index) => (Seed: seed, Index: index))
            .ToDictionary(item => item.Seed.Isbn13, StringComparer.Ordinal);
        if (seedByIsbn.Count == 0)
        {
            return [];
        }

        var scoresByCandidate = new Dictionary<string, CandidateScore>(StringComparer.Ordinal);
        foreach (var neighbor in neighbors)
        {
            if (!seedByIsbn.TryGetValue(neighbor.SeedIsbn13, out var seed) ||
                !Isbn13.TryCreate(neighbor.NeighborIsbn13, out _))
            {
                continue;
            }

            if (!scoresByCandidate.TryGetValue(neighbor.NeighborIsbn13, out var candidate))
            {
                candidate = new CandidateScore();
                scoresByCandidate.Add(neighbor.NeighborIsbn13, candidate);
            }

            if (!candidate.SeenSeeds.Add(neighbor.SeedIsbn13))
            {
                continue;
            }

            candidate.TotalScore += neighbor.Score;
            if (neighbor.Score >= settings.SimilarMinScore)
            {
                candidate.ContributorCount++;
            }

            if (neighbor.Score > candidate.BestScore ||
                (neighbor.Score == candidate.BestScore && seed.Index < candidate.BestSeedIndex))
            {
                candidate.BestScore = neighbor.Score;
                candidate.BestSeedIndex = seed.Index;
                candidate.SeedTitle = seed.Seed.Title ?? seed.Seed.Isbn13;
                candidate.Reason = neighbor.Reason;
            }
        }

        var excludedIsbns = new HashSet<string>(exclusions.PurchasedIsbns, StringComparer.Ordinal);
        excludedIsbns.UnionWith(exclusions.FavoriteIsbns);
        excludedIsbns.UnionWith(exclusions.SelectionIsbns);
        excludedIsbns.UnionWith(seedByIsbn.Keys);
        var excludedWorkIds = new HashSet<string>(exclusions.PurchasedWorkIds, StringComparer.Ordinal);
        excludedWorkIds.UnionWith(exclusions.FavoriteWorkIds);
        var publicBooksByIsbn = publicBooks
            .Where(book => book.QuantityAvailable > 0 || book.QuantityAnnounced > 0)
            .Where(book => arrivedAfter is null || book.FirstSeenAt > arrivedAfter.Value)
            .Where(book => !excludedIsbns.Contains(book.Isbn13))
            .Where(book => book.WorkId is null || !excludedWorkIds.Contains(book.WorkId))
            .ToDictionary(book => book.Isbn13, StringComparer.Ordinal);

        var ordered = scoresByCandidate
            .Where(pair =>
                pair.Value.ContributorCount > 0 &&
                publicBooksByIsbn.ContainsKey(pair.Key))
            .Select(pair =>
            {
                var hearts = Math.Clamp(
                    HeartThresholds.Count(threshold => threshold <= pair.Value.BestScore),
                    1,
                    5);
                if (pair.Value.ContributorCount >= 2)
                {
                    hearts = Math.Min(5, hearts + 1);
                }

                return new
                {
                    pair.Key,
                    Score = pair.Value,
                    Hearts = hearts,
                    Book = publicBooksByIsbn[pair.Key],
                };
            })
            .Where(item => item.Hearts >= minimumHearts)
            .OrderByDescending(item => item.Hearts)
            .ThenByDescending(item => item.Score.TotalScore)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .Take(limit)
            .Select(item => new PersonalRecommendation(
                item.Book,
                item.Score.Reason,
                item.Score.SeedTitle,
                item.Hearts,
                Math.Max(0, item.Score.ContributorCount - 1)))
            .ToArray();

        return ordered;
    }

    private IReadOnlyList<float> HeartThresholds =>
        settings.HeartThresholds is { Count: > 0 } configured
            ? configured
            : FallbackHeartThresholds;

    private sealed class CandidateScore
    {
        public HashSet<string> SeenSeeds { get; } = new(StringComparer.Ordinal);
        public float TotalScore { get; set; }
        public float BestScore { get; set; } = float.NegativeInfinity;
        public int ContributorCount { get; set; }
        public int BestSeedIndex { get; set; } = int.MaxValue;
        public string SeedTitle { get; set; } = string.Empty;
        public NeighborReason Reason { get; set; }
    }
}
