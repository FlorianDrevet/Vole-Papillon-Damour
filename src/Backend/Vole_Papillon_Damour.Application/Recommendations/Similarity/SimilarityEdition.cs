namespace Vole_Papillon_Damour.Application.Recommendations.Similarity;

/// <summary>Ce que les sources disent d'une édition. Sérialisé en JSON dans BookSimilarityProfile.NoticeJson.</summary>
public sealed record SimilarityEdition(
    string Isbn13,
    string Title,
    string? PartTitle,
    int? PartNumber,
    string? Subtitle,
    IReadOnlyList<string> Authors,
    IReadOnlyList<string> AuthorSurnames,
    string? Publisher,
    string? Collection,
    string? SeriesTitle,
    int? SeriesNumber,
    string? BnfWorkId,
    string? OpenLibraryWorkKey,
    IReadOnlyList<string> Forms,
    IReadOnlyList<string> Subjects,
    string? Audience333,
    bool CnljReviewed,
    string? Dewey,
    string? EditionSummary,
    string? OpenLibraryDescription,
    IReadOnlyList<string>? Languages = null);
