namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdminBooksImportResult(
    int RequestedCount,
    int CreatedCount,
    int AlreadyPresentCount,
    IReadOnlyList<AdminBooksImportResult.Item> Results)
{
    public sealed record Item(string Isbn13, Status Status);

    public enum Status
    {
        Created,
        AlreadyPresent
    }
}
