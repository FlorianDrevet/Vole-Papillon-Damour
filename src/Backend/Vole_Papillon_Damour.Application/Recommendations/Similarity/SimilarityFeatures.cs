using System.Text.RegularExpressions;

namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

public enum SimilarityAudience { Adult = 0, Youth = 1, Teen = 2 }

public enum SimilarityForm { Text = 0, Illustrated = 1, Essay = 2 }

public sealed record SimilarityFeatures(
    string Isbn13,
    string TitleKey,
    IReadOnlySet<string> Surnames,
    string? BnfWorkId,
    string? OpenLibraryWorkKey,
    string? SeriesKey,
    int? Tome,
    SimilarityAudience Audience,
    SimilarityForm Form);

public static partial class SimilarityFeatureBuilder
{
    // Collections et éditeurs jeunesse, en forme normalisée. Liste à compléter au fil des erreurs observées.
    private static readonly string[] YouthMarkers =
    [
        "junior", "jeunesse", "cadet", "bibliotheque rose", "bibliotheque verte", "j aime lire", "castor",
        "witty", "wiz", "kids", "premiers romans", "mon premier", "heure des histoires", "albums", "fj poche",
    ];

    private static readonly string[] MangaPublishers = ["kana", "pika", "kurokawa", "ki oon", "kaze", "tonkam", "glenat manga"];

    private static readonly string[] ComicPublishers =
    [
        "dargaud", "dupuis", "casterman", "lombard", "delcourt", "soleil", "bamboo", "futuropolis",
        "l association", "rue de sevres", "blake et mortimer", "moulinsart", "fluide glacial", "vents d ouest",
    ];

    [GeneratedRegex(@"(\d+)\s*ans")]
    private static partial Regex AgeRegex();

    public static SimilarityAudience InferAudience(SimilarityEdition edition)
    {
        if (edition.Audience333 is { } audience && AgeRegex().Match(audience) is { Success: true } match)
        {
            return int.Parse(match.Groups[1].Value) <= 12 ? SimilarityAudience.Youth : SimilarityAudience.Teen;
        }

        var haystack = SimilarityText.Normalize($"{edition.Collection} {edition.Publisher}");
        if (edition.CnljReviewed || YouthMarkers.Any(marker => ContainsWord(haystack, marker)))
        {
            return SimilarityAudience.Youth;
        }

        return SimilarityAudience.Adult;
    }

    public static SimilarityForm InferForm(SimilarityEdition edition)
    {
        var forms = SimilarityText.Normalize(string.Join(' ', edition.Forms));
        var publisher = SimilarityText.Normalize(edition.Publisher);
        var collection = SimilarityText.Normalize(edition.Collection);
        if (collection.Contains("manga") || MangaPublishers.Any(publisher.Contains) ||
            forms.Contains("bandes dessinees") || forms.Contains("romans graphiques") || forms.Contains("manga") ||
            ComicPublishers.Any(publisher.Contains))
        {
            return SimilarityForm.Illustrated;
        }

        var dewey = edition.Dewey?.Trim();
        if (!string.IsNullOrEmpty(dewey) && !dewey.StartsWith('8'))
        {
            return SimilarityForm.Essay;
        }

        return edition.Subjects.Count > 0 && !forms.Contains("roman") ? SimilarityForm.Essay : SimilarityForm.Text;
    }

    public static (string? Key, int? Tome) InferSeries(SimilarityEdition edition)
    {
        if (!string.IsNullOrWhiteSpace(edition.SeriesTitle))
        {
            return (SimilarityText.Normalize(edition.SeriesTitle), edition.SeriesNumber ?? edition.PartNumber);
        }

        return edition.PartNumber is { } part ? (SimilarityText.Normalize(edition.Title), part) : (null, null);
    }

    public static SimilarityFeatures Build(SimilarityEdition edition, SimilarityAudience? audienceOverride = null)
    {
        var (seriesKey, tome) = InferSeries(edition);
        return new SimilarityFeatures(
            edition.Isbn13,
            SimilarityText.Normalize(edition.Title),
            edition.AuthorSurnames.Select(SimilarityText.Normalize).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal),
            string.IsNullOrWhiteSpace(edition.BnfWorkId) ? null : edition.BnfWorkId,
            string.IsNullOrWhiteSpace(edition.OpenLibraryWorkKey) ? null : edition.OpenLibraryWorkKey,
            seriesKey,
            tome,
            audienceOverride ?? InferAudience(edition),
            InferForm(edition));
    }

    public static bool SameWork(SimilarityFeatures a, SimilarityFeatures b)
    {
        if (a.Tome is { } tomeA && b.Tome is { } tomeB && tomeA != tomeB)
        {
            return false; // la BnF donne parfois à tous les tomes l'identifiant de la série
        }

        if (a.BnfWorkId is not null && a.BnfWorkId == b.BnfWorkId)
        {
            return true;
        }

        if (a.OpenLibraryWorkKey is not null && a.OpenLibraryWorkKey == b.OpenLibraryWorkKey)
        {
            return true;
        }

        return a.TitleKey == b.TitleKey &&
               (a.Surnames.Count == 0 || b.Surnames.Count == 0 || a.Surnames.Overlaps(b.Surnames));
    }

    public static string ComposeText(SimilarityEdition edition, string? workSummary)
    {
        var parts = new List<string> { TitleLine(edition) };
        if (edition.Authors.Count > 0)
        {
            parts.Add("Auteur : " + string.Join(", ", edition.Authors));
        }

        if (!string.IsNullOrWhiteSpace(edition.Collection))
        {
            parts.Add("Collection : " + edition.Collection);
        }

        if (!string.IsNullOrWhiteSpace(edition.SeriesTitle))
        {
            parts.Add("Série : " + edition.SeriesTitle + (edition.SeriesNumber is { } n ? $", tome {n}" : string.Empty));
        }

        if (edition.Forms.Count > 0)
        {
            parts.Add("Genre : " + string.Join(", ", edition.Forms.Distinct()));
        }

        if (edition.Subjects.Count > 0)
        {
            parts.Add("Sujets : " + string.Join(", ", edition.Subjects.Distinct()));
        }

        if (!string.IsNullOrWhiteSpace(edition.Audience333))
        {
            parts.Add("Public : " + edition.Audience333);
        }

        if (!string.IsNullOrWhiteSpace(workSummary))
        {
            parts.Add(workSummary);
        }

        return string.Join(". ", parts);
    }

    private static string TitleLine(SimilarityEdition edition)
    {
        var title = edition.Title;
        if (!string.IsNullOrWhiteSpace(edition.PartTitle) &&
            SimilarityText.Normalize(edition.PartTitle) != SimilarityText.Normalize(title))
        {
            title += ", " + edition.PartTitle;
        }

        return string.IsNullOrWhiteSpace(edition.Subtitle) ? title : $"{title} : {edition.Subtitle}";
    }

    private static bool ContainsWord(string haystack, string marker) =>
        $" {haystack} ".Contains($" {marker} ", StringComparison.Ordinal);
}
