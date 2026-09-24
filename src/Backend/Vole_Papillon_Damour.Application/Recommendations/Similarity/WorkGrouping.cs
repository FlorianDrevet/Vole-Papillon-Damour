namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public sealed record GroupedEdition(SimilarityEdition Edition, SimilarityFeatures Features, string? WorkSummary);

public static class WorkGrouping
{
    public static IReadOnlyList<GroupedEdition> Group(IReadOnlyList<SimilarityEdition> editions)
    {
        var raw = editions.Select(e => SimilarityFeatureBuilder.Build(e)).ToArray();
        var parent = Enumerable.Range(0, editions.Count).ToArray();

        int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

        // Regroupement par clés exactes pour rester en O(n) : œuvre BnF, œuvre OL, titre normalisé.
        void UnionBy(Func<SimilarityFeatures, string?> key)
        {
            foreach (var bucket in raw.Select((f, i) => (Key: key(f), Index: i))
                         .Where(x => x.Key is not null)
                         .GroupBy(x => x.Key, StringComparer.Ordinal))
            {
                var members = bucket.Select(x => x.Index).ToArray();
                for (var a = 0; a < members.Length; a++)
                for (var b = a + 1; b < members.Length; b++)
                {
                    if (SimilarityFeatureBuilder.SameWork(raw[members[a]], raw[members[b]]))
                    {
                        parent[Find(members[a])] = Find(members[b]);
                    }
                }
            }
        }

        UnionBy(f => f.BnfWorkId);
        UnionBy(f => f.OpenLibraryWorkKey);
        UnionBy(f => f.TitleKey);

        var groups = Enumerable.Range(0, editions.Count).GroupBy(Find).ToArray();
        var result = new GroupedEdition[editions.Count];
        foreach (var group in groups)
        {
            var members = group.ToArray();
            var summary = members
                .Select(i => SimilarityText.CleanSummary(editions[i].EditionSummary, editions[i].Title))
                .Where(s => s is not null)
                .OrderByDescending(s => s!.Length)
                .FirstOrDefault()
                ?? members
                    .Select(i => SimilarityText.CleanSummary(editions[i].OpenLibraryDescription, editions[i].Title))
                    .FirstOrDefault(s => s is not null);
            var audience = members
                .GroupBy(i => raw[i].Audience)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .First()
                .Key;
            foreach (var i in members)
            {
                result[i] = new GroupedEdition(editions[i], raw[i] with { Audience = audience }, summary);
            }
        }

        return result;
    }
}
