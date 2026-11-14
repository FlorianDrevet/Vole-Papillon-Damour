using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;
using Microsoft.Extensions.Options;

namespace Vole_Papillon_Damour.Application.FairDigest.Common;

public sealed record FairDigestBackItem(
    PublicCatalogBookResult Book,
    int SalesDuringPreviousFair,
    bool IsVeryDemanded);

public sealed record FairDigestMemberContent(
    IReadOnlyList<PersonalRecommendation> News,
    IReadOnlyList<FairDigestBackItem> Back,
    bool SkippedNoContent);

public sealed class FairDigestContentBuilder(IOptions<FairDigestOptions> options)
{
    public FairDigestMemberContent Build(
        IEnumerable<PersonalRecommendation> news,
        IEnumerable<FairDigestBackItem> back)
    {
        ArgumentNullException.ThrowIfNull(news);
        ArgumentNullException.ThrowIfNull(back);

        var selectedNews = DistinctByWork(news, recommendation => recommendation.Book)
            .Take(options.Value.NewsMax)
            .ToArray();
        var newsWorkKeys = selectedNews
            .Select(recommendation => WorkKey(recommendation.Book))
            .ToHashSet(StringComparer.Ordinal);
        var selectedBack = new List<FairDigestBackItem>();
        var seenBackKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in back
                     .OrderByDescending(candidate => candidate.SalesDuringPreviousFair)
                     .ThenBy(candidate => candidate.Book.Isbn13, StringComparer.Ordinal))
        {
            var key = WorkKey(item.Book);
            if (newsWorkKeys.Contains(key) || !seenBackKeys.Add(key))
            {
                continue;
            }

            selectedBack.Add(item);
            if (selectedBack.Count == options.Value.BackMax)
            {
                break;
            }
        }

        return new FairDigestMemberContent(
            selectedNews,
            selectedBack,
            selectedNews.Length == 0 && selectedBack.Count < options.Value.BackMinCount);
    }

    private static IEnumerable<T> DistinctByWork<T>(
        IEnumerable<T> items,
        Func<T, PublicCatalogBookResult> getBook)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (seen.Add(WorkKey(getBook(item))))
            {
                yield return item;
            }
        }
    }

    private static string WorkKey(PublicCatalogBookResult book) =>
        string.IsNullOrWhiteSpace(book.WorkId)
            ? $"isbn:{book.Isbn13}"
            : $"work:{book.WorkId}";
}
