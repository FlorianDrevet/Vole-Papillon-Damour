using Vole_Papillon_Damour.Application.Recommendations.Similarity;

namespace Vole_Papillon_Damour.Application.Isbndb;

public static class IsbndbGenreMapper
{
    private static readonly string[] GenreOrder =
    [
        "Roman",
        "Science-fiction",
        "Fantasy",
        "Bande dessinée",
        "Manga",
        "Policier",
        "Thriller",
        "Romance",
        "Histoire",
        "Biographie",
        "Jeunesse",
        "Essai",
        "Théâtre",
        "Poésie",
        "Cuisine",
        "Documentaire"
    ];

    private static readonly IReadOnlyDictionary<string, string> SubjectGenres =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["romans"] = "Roman",
            ["roman"] = "Roman",
            ["fiction"] = "Roman",
            ["literary"] = "Roman",
            ["literature fiction"] = "Roman",
            ["classics"] = "Roman",
            ["genre fiction"] = "Roman",
            ["science fiction"] = "Science-fiction",
            ["science fiction fantasy"] = "Science-fiction",
            ["fantasy"] = "Fantasy",
            ["bandes dessinees"] = "Bande dessinée",
            ["comics"] = "Bande dessinée",
            ["graphic novels"] = "Bande dessinée",
            ["comics graphic novels"] = "Bande dessinée",
            ["manga"] = "Manga",
            ["mangas"] = "Manga",
            ["mystery"] = "Policier",
            ["policiers"] = "Policier",
            ["romans policiers"] = "Policier",
            ["crime"] = "Policier",
            ["detective"] = "Policier",
            ["thriller"] = "Thriller",
            ["thrillers"] = "Thriller",
            ["suspense"] = "Thriller",
            ["romance"] = "Romance",
            ["romans sentimentaux"] = "Romance",
            ["history"] = "Histoire",
            ["histoire"] = "Histoire",
            ["historical"] = "Histoire",
            ["biography"] = "Biographie",
            ["biographies"] = "Biographie",
            ["biography autobiography"] = "Biographie",
            ["memoires"] = "Biographie",
            ["juvenile fiction"] = "Jeunesse",
            ["childrens books"] = "Jeunesse",
            ["children s books"] = "Jeunesse",
            ["jeunesse"] = "Jeunesse",
            ["albums jeunesse"] = "Jeunesse",
            ["young adult"] = "Jeunesse",
            ["essays"] = "Essai",
            ["essais"] = "Essai",
            ["philosophy"] = "Essai",
            ["philosophie"] = "Essai",
            ["drama"] = "Théâtre",
            ["dramas plays"] = "Théâtre",
            ["theatre"] = "Théâtre",
            ["poetry"] = "Poésie",
            ["poesie"] = "Poésie",
            ["cooking"] = "Cuisine",
            ["cookbooks"] = "Cuisine",
            ["cuisine"] = "Cuisine",
            ["reference"] = "Documentaire",
            ["study aids"] = "Documentaire",
            ["education"] = "Documentaire",
            ["travel"] = "Documentaire",
            ["nature"] = "Documentaire",
            ["science"] = "Documentaire"
        };

    private static readonly HashSet<string> IgnoredSubjects =
    [
        "self service",
        "arborist merchandising root",
        "subjects"
    ];

    public static (IReadOnlyList<string> Genres, IReadOnlyList<string> UnmatchedSubjects) Map(
        IReadOnlyList<string>? subjects,
        string? publisher,
        IReadOnlyList<string>? authors)
    {
        var normalizedAuthors = BuildAuthorKeys(authors);
        var matchedGenres = new HashSet<string>(StringComparer.Ordinal);
        var unmatchedSubjects = new List<string>();
        var seenUnmatched = new HashSet<string>(StringComparer.Ordinal);
        var normalizedSubjectKeys = (subjects ?? [])
            .Select(subject => new { Value = subject?.Trim() ?? "", Key = SimilarityText.Normalize(subject) })
            .Where(subject => subject.Value.Length > 0)
            .ToArray();
        var hasFantasySubject = normalizedSubjectKeys.Any(subject =>
            GetSegments(subject.Value).Any(segment => segment == "fantasy"));

        foreach (var subject in normalizedSubjectKeys)
        {
            if (IgnoredSubjects.Contains(subject.Key) || normalizedAuthors.Contains(subject.Key))
            {
                continue;
            }

            var subjectGenres = MapSubject(subject.Value, subject.Key, hasFantasySubject);
            if (subjectGenres.Count > 0)
            {
                matchedGenres.UnionWith(subjectGenres);
            }
            else if (seenUnmatched.Add(subject.Key))
            {
                unmatchedSubjects.Add(subject.Value);
            }
        }

        if (SimilarityFeatureBuilder.IsComicPublisher(publisher))
        {
            matchedGenres.Add("Bande dessinée");
        }
        else if (SimilarityFeatureBuilder.IsMangaPublisher(publisher))
        {
            matchedGenres.Add("Manga");
        }

        if (matchedGenres.Any(genre => genre != "Roman"))
        {
            matchedGenres.Remove("Roman");
        }

        var genres = GenreOrder
            .Where(matchedGenres.Contains)
            .Take(3)
            .ToArray();
        return (genres, unmatchedSubjects);
    }

    private static IReadOnlySet<string> BuildAuthorKeys(IReadOnlyList<string>? authors)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var author in authors ?? [])
        {
            var trimmed = author.Trim();
            var normalized = SimilarityText.Normalize(trimmed);
            if (normalized.Length == 0)
            {
                continue;
            }

            keys.Add(normalized);
            var comma = trimmed.IndexOf(',');
            if (comma > 0)
            {
                var surname = SimilarityText.Normalize(trimmed[..comma]);
                if (surname.Length > 0)
                {
                    keys.Add(surname);
                }
            }
            else
            {
                var finalName = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (finalName is not null)
                {
                    keys.Add(SimilarityText.Normalize(finalName));
                }
            }
        }

        return keys;
    }

    private static IReadOnlyList<string> MapSubject(
        string subject,
        string normalizedSubject,
        bool hasFantasySubject)
    {
        var genres = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in GetSegments(subject)
                     .Prepend(normalizedSubject)
                     .Distinct(StringComparer.Ordinal))
        {
            if (segment == "science fiction fantasy")
            {
                genres.Add("Science-fiction");
                genres.Add("Fantasy");
                continue;
            }

            if (segment == "epic")
            {
                if (hasFantasySubject)
                {
                    genres.Add("Fantasy");
                }

                continue;
            }

            if (SubjectGenres.TryGetValue(segment, out var genre))
            {
                genres.Add(genre);
            }
        }

        return GenreOrder.Where(genres.Contains).ToArray();
    }

    private static IEnumerable<string> GetSegments(string subject) =>
        subject
            .Split(['&', ';', '>', '|', '/', ',', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(SimilarityText.Normalize)
            .Where(segment => segment.Length > 0);
}
