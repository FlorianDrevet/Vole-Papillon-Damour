namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BookCoverOptions
{
    public const string SectionName = "Bibliographic:Covers";

    public bool Enabled { get; init; } = true;
    public int MaxBytes { get; init; } = 5 * 1024 * 1024;
    public string ContainerPrefix { get; init; } = "books/covers";
    public string[] AllowedHosts { get; init; } =
    [
        "covers.openlibrary.org",
        "openapi.bnf.fr"
    ];
}
