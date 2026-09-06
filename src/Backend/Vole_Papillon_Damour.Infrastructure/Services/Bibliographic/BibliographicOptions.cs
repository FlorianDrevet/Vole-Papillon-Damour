namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicOptions
{
    public const string SectionName = "Bibliographic";

    public string BnfSruEndpoint { get; set; } = "https://catalogue.bnf.fr/api/SRU";
    public string OpenLibrarySearchEndpoint { get; set; } = "https://openlibrary.org/search.json";
    public string OpenLibraryCoverEndpoint { get; set; } = "https://covers.openlibrary.org/b/id/{0}-L.jpg?default=false";
    public string GoogleBooksEndpoint { get; set; } = "https://www.googleapis.com/books/v1/volumes";
    public string? GoogleBooksApiKey { get; set; }
    public string UserAgent { get; set; } = "Vole-Papillon-d-Amour/1.0 (contact@volepapillondamour.fr)";
    public int BnfTimeoutMilliseconds { get; set; } = 800;
    public int OpenLibraryTimeoutMilliseconds { get; set; } = 5_000;
    public int GoogleBooksTimeoutMilliseconds { get; set; } = 5_000;
}
